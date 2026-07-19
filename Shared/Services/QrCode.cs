namespace OnceDeOro.Services;

/// <summary>
/// Generador de códigos QR, escrito acá adentro a propósito: el juego es una
/// página estática y no quiero que dependa de un CDN externo ni que el navegador
/// del jugador le pida el QR a un tercero (sería mandarle a otro el código de tu
/// sala). Sin dependencias, sin red y sin nada que auditar de afuera.
///
/// Alcance deliberadamente chico: modo BYTE, corrección de errores M y versiones
/// 1 a 6 (hasta 108 bytes de contenido). Alcanza de sobra para una URL de sala y
/// evita tener que emitir la "version information" que exigen las versiones 7+.
/// </summary>
public static class QrCode
{
    // ---- Tabla por versión (nivel M) ----
    // total   = codewords totales del símbolo
    // ecPerB  = codewords de corrección por bloque
    // g1/g2   = cantidad de bloques y codewords de datos de cada grupo
    private readonly record struct Ver(int Total, int EcPerB, int G1, int G1Data, int G2, int G2Data)
    {
        public int DataCodewords => G1 * G1Data + G2 * G2Data;
    }

    private static readonly Ver[] Versions =
    {
        default,                              // índice 0 sin usar
        new(26,  10, 1, 16, 0, 0),            // v1  → 16 bytes
        new(44,  16, 1, 28, 0, 0),            // v2  → 28
        new(70,  26, 1, 44, 0, 0),            // v3  → 44
        new(100, 18, 2, 32, 0, 0),            // v4  → 64
        new(134, 24, 2, 43, 0, 0),            // v5  → 86
        new(172, 16, 4, 27, 0, 0),            // v6  → 108
    };

    /// <summary>Centros de los patrones de alineación por versión.</summary>
    private static readonly int[][] AlignCenters =
    {
        Array.Empty<int>(),                   // 0
        Array.Empty<int>(),                   // v1: no lleva
        new[] { 6, 18 },
        new[] { 6, 22 },
        new[] { 6, 26 },
        new[] { 6, 30 },
        new[] { 6, 34 },
    };

    public const int MaxBytes = 108;

    /// <summary>
    /// Codifica el texto y devuelve la matriz de módulos: true = negro.
    /// El índice es [fila, columna].
    /// </summary>
    public static bool[,] Encode(string text)
    {
        var data = System.Text.Encoding.UTF8.GetBytes(text);
        if (data.Length > MaxBytes)
            throw new ArgumentException($"El QR de este juego llega hasta {MaxBytes} bytes.", nameof(text));

        int version = PickVersion(data.Length);
        var v = Versions[version];
        int size = version * 4 + 17;

        var codewords = BuildCodewords(data, v);

        // Se prueban las 8 máscaras y se elige la de menor penalización, que es lo
        // que hace que el lector no se confunda con zonas muy uniformes.
        bool[,]? mejor = null;
        int mejorPenal = int.MaxValue, mejorMask = 0;
        for (int mask = 0; mask < 8; mask++)
        {
            var m = new bool[size, size];
            var reservado = new bool[size, size];
            DrawFunctionPatterns(m, reservado, version, size);
            PlaceData(m, reservado, codewords, size);
            ApplyMask(m, reservado, mask, size);
            DrawFormat(m, mask, size);

            int p = Penalty(m, size);
            if (p < mejorPenal) { mejorPenal = p; mejor = m; mejorMask = mask; }
        }

        _ = mejorMask;
        return mejor!;
    }

    private static int PickVersion(int len)
    {
        // 4 bits de modo + 8 bits de longitud (byte mode, versiones 1-9) + los datos
        int bits = 4 + 8 + len * 8;
        int needed = (bits + 7) / 8;
        for (int v = 1; v < Versions.Length; v++)
            if (Versions[v].DataCodewords >= needed) return v;
        throw new ArgumentException("Texto demasiado largo para este generador.");
    }

    // ---------------------------------------------------------------- datos + ECC
    private static byte[] BuildCodewords(byte[] data, Ver v)
    {
        var bits = new List<bool>();
        void Put(int value, int count)
        {
            for (int i = count - 1; i >= 0; i--) bits.Add(((value >> i) & 1) == 1);
        }

        Put(0b0100, 4);            // modo byte
        Put(data.Length, 8);       // longitud
        foreach (var b in data) Put(b, 8);

        int capacity = v.DataCodewords * 8;
        for (int i = 0; i < 4 && bits.Count < capacity; i++) bits.Add(false);   // terminador
        while (bits.Count % 8 != 0) bits.Add(false);

        var dataCw = new List<byte>();
        for (int i = 0; i < bits.Count; i += 8)
        {
            int b = 0;
            for (int j = 0; j < 8; j++) b = (b << 1) | (bits[i + j] ? 1 : 0);
            dataCw.Add((byte)b);
        }
        // Relleno alternando 236/17, como manda la norma
        byte[] pad = { 0xEC, 0x11 };
        for (int i = 0; dataCw.Count < v.DataCodewords; i++) dataCw.Add(pad[i % 2]);

        // Se parte en bloques, cada uno con su corrección
        var bloquesDatos = new List<byte[]>();
        var bloquesEc = new List<byte[]>();
        int pos = 0;
        for (int i = 0; i < v.G1 + v.G2; i++)
        {
            int n = i < v.G1 ? v.G1Data : v.G2Data;
            var bloque = dataCw.GetRange(pos, n).ToArray();
            pos += n;
            bloquesDatos.Add(bloque);
            bloquesEc.Add(ReedSolomon(bloque, v.EcPerB));
        }

        // Y se intercalan: primero los datos columna por columna, después la ECC
        var salida = new List<byte>();
        int maxData = Math.Max(v.G1Data, v.G2 > 0 ? v.G2Data : 0);
        for (int i = 0; i < maxData; i++)
            foreach (var b in bloquesDatos)
                if (i < b.Length) salida.Add(b[i]);
        for (int i = 0; i < v.EcPerB; i++)
            foreach (var b in bloquesEc)
                salida.Add(b[i]);

        return salida.ToArray();
    }

    // ---- Aritmética en GF(256) para Reed-Solomon ----
    private static readonly byte[] Exp = new byte[512];
    private static readonly byte[] Log = new byte[256];

    static QrCode()
    {
        int x = 1;
        for (int i = 0; i < 255; i++)
        {
            Exp[i] = (byte)x;
            Log[x] = (byte)i;
            x <<= 1;
            if ((x & 0x100) != 0) x ^= 0x11D;    // polinomio primitivo del QR
        }
        for (int i = 255; i < 512; i++) Exp[i] = Exp[i - 255];
    }

    private static byte Mul(byte a, byte b) =>
        a == 0 || b == 0 ? (byte)0 : Exp[Log[a] + Log[b]];

    private static byte[] ReedSolomon(byte[] data, int ecLen)
    {
        // Polinomio generador
        var gen = new byte[ecLen + 1];
        gen[0] = 1;
        for (int i = 0; i < ecLen; i++)
        {
            for (int j = i; j >= 0; j--)
            {
                gen[j + 1] ^= Mul(gen[j], Exp[i]);
            }
        }

        var resto = new byte[ecLen];
        foreach (var d in data)
        {
            byte factor = (byte)(d ^ resto[0]);
            Array.Copy(resto, 1, resto, 0, ecLen - 1);
            resto[ecLen - 1] = 0;
            for (int i = 0; i < ecLen; i++)
                resto[i] ^= Mul(gen[i + 1], factor);
        }
        return resto;
    }

    // ---------------------------------------------------------------- dibujo
    private static void DrawFunctionPatterns(bool[,] m, bool[,] res, int version, int size)
    {
        // Los tres ojos de las esquinas, con su separador
        foreach (var (r, c) in new[] { (0, 0), (0, size - 7), (size - 7, 0) })
            DrawFinder(m, res, r, c, size);

        // Patrones de alineación (nunca encima de un ojo)
        var centers = AlignCenters[version];
        foreach (int r in centers)
            foreach (int c in centers)
            {
                if ((r <= 8 && c <= 8) || (r <= 8 && c >= size - 9) || (r >= size - 9 && c <= 8)) continue;
                DrawAlignment(m, res, r, c);
            }

        // Líneas de tiempo
        for (int i = 8; i < size - 8; i++)
        {
            bool on = i % 2 == 0;
            m[6, i] = on; res[6, i] = true;
            m[i, 6] = on; res[i, 6] = true;
        }

        // Módulo oscuro fijo
        m[size - 8, 8] = true; res[size - 8, 8] = true;

        // Zonas reservadas para el formato
        for (int i = 0; i < 9; i++)
        {
            if (!res[8, i]) res[8, i] = true;
            if (!res[i, 8]) res[i, 8] = true;
        }
        for (int i = 0; i < 8; i++)
        {
            res[8, size - 1 - i] = true;
            res[size - 1 - i, 8] = true;
        }
    }

    private static void DrawFinder(bool[,] m, bool[,] res, int row, int col, int size)
    {
        for (int r = -1; r <= 7; r++)
            for (int c = -1; c <= 7; c++)
            {
                int rr = row + r, cc = col + c;
                if (rr < 0 || rr >= size || cc < 0 || cc >= size) continue;
                bool on = (r >= 0 && r <= 6 && (c == 0 || c == 6)) ||
                          (c >= 0 && c <= 6 && (r == 0 || r == 6)) ||
                          (r >= 2 && r <= 4 && c >= 2 && c <= 4);
                m[rr, cc] = on;
                res[rr, cc] = true;
            }
    }

    private static void DrawAlignment(bool[,] m, bool[,] res, int row, int col)
    {
        for (int r = -2; r <= 2; r++)
            for (int c = -2; c <= 2; c++)
            {
                m[row + r, col + c] = Math.Max(Math.Abs(r), Math.Abs(c)) != 1;
                res[row + r, col + c] = true;
            }
    }

    /// <summary>Recorrido en zigzag de abajo hacia arriba, de a dos columnas.</summary>
    private static void PlaceData(bool[,] m, bool[,] res, byte[] cw, int size)
    {
        int bit = 0;
        int total = cw.Length * 8;

        for (int right = size - 1; right >= 1; right -= 2)
        {
            if (right == 6) right = 5;          // la columna de tiempo no cuenta
            for (int i = 0; i < size; i++)
            {
                bool subiendo = ((size - 1 - right) / 2) % 2 == 0;
                int row = subiendo ? size - 1 - i : i;
                for (int j = 0; j < 2; j++)
                {
                    int col = right - j;
                    if (res[row, col]) continue;
                    bool on = bit < total && ((cw[bit / 8] >> (7 - bit % 8)) & 1) == 1;
                    m[row, col] = on;
                    bit++;
                }
            }
        }
    }

    private static bool MaskAt(int mask, int r, int c) => mask switch
    {
        0 => (r + c) % 2 == 0,
        1 => r % 2 == 0,
        2 => c % 3 == 0,
        3 => (r + c) % 3 == 0,
        4 => (r / 2 + c / 3) % 2 == 0,
        5 => (r * c) % 2 + (r * c) % 3 == 0,
        6 => ((r * c) % 2 + (r * c) % 3) % 2 == 0,
        _ => ((r + c) % 2 + (r * c) % 3) % 2 == 0,
    };

    private static void ApplyMask(bool[,] m, bool[,] res, int mask, int size)
    {
        for (int r = 0; r < size; r++)
            for (int c = 0; c < size; c++)
                if (!res[r, c] && MaskAt(mask, r, c))
                    m[r, c] = !m[r, c];
    }

    /// <summary>Los 15 bits de formato (nivel M + máscara), con su BCH y su XOR fijo.</summary>
    private static void DrawFormat(bool[,] m, int mask, int size)
    {
        const int EccM = 0b00;                 // nivel M
        int datos = (EccM << 3) | mask;
        int rem = datos;
        for (int i = 0; i < 10; i++)
            rem = (rem << 1) ^ (((rem >> 9) & 1) * 0x537);
        int formato = ((datos << 10) | rem) ^ 0x5412;

        for (int i = 0; i < 15; i++)
        {
            bool on = ((formato >> i) & 1) == 1;

            // Copia junto al ojo superior izquierdo
            if (i < 6) m[8, i] = on;
            else if (i == 6) m[8, 7] = on;
            else if (i == 7) m[8, 8] = on;
            else if (i == 8) m[7, 8] = on;
            else m[14 - i, 8] = on;

            // Copia repartida entre los otros dos ojos
            if (i < 8) m[size - 1 - i, 8] = on;
            else m[8, size - 15 + i] = on;
        }
        m[size - 8, 8] = true;                 // el módulo oscuro siempre gana
    }

    // ---------------------------------------------------------------- penalización
    private static int Penalty(bool[,] m, int size)
    {
        int total = 0;

        // 1) Tiras de 5 o más del mismo color
        for (int r = 0; r < size; r++)
            for (int pasada = 0; pasada < 2; pasada++)
            {
                int run = 1;
                for (int c = 1; c < size; c++)
                {
                    bool a = pasada == 0 ? m[r, c] : m[c, r];
                    bool b = pasada == 0 ? m[r, c - 1] : m[c - 1, r];
                    if (a == b) run++;
                    else { if (run >= 5) total += 3 + (run - 5); run = 1; }
                }
                if (run >= 5) total += 3 + (run - 5);
            }

        // 2) Bloques de 2x2 del mismo color
        for (int r = 0; r < size - 1; r++)
            for (int c = 0; c < size - 1; c++)
                if (m[r, c] == m[r, c + 1] && m[r, c] == m[r + 1, c] && m[r, c] == m[r + 1, c + 1])
                    total += 3;

        // 3) Patrones que se parecen a un ojo (1:1:3:1:1 con zona clara)
        bool[] p1 = { true, false, true, true, true, false, true, false, false, false, false };
        bool[] p2 = { false, false, false, false, true, false, true, true, true, false, true };
        for (int r = 0; r < size; r++)
            for (int c = 0; c <= size - 11; c++)
            {
                bool okH1 = true, okH2 = true, okV1 = true, okV2 = true;
                for (int i = 0; i < 11; i++)
                {
                    if (m[r, c + i] != p1[i]) okH1 = false;
                    if (m[r, c + i] != p2[i]) okH2 = false;
                    if (m[c + i, r] != p1[i]) okV1 = false;
                    if (m[c + i, r] != p2[i]) okV2 = false;
                }
                if (okH1) total += 40;
                if (okH2) total += 40;
                if (okV1) total += 40;
                if (okV2) total += 40;
            }

        // 4) Desbalance entre claros y oscuros
        int oscuros = 0;
        foreach (bool b in m) if (b) oscuros++;
        int porcentaje = oscuros * 100 / (size * size);
        total += Math.Abs(porcentaje - 50) / 5 * 10;

        return total;
    }
}
