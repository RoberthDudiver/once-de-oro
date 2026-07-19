// ============================================================
//  Sonidos de Once de Oro — TODO generado por código (Web Audio).
//  Sin archivos, sin licencias y sin atribución: nada que descargar.
// ============================================================
(() => {
    const KEY = 'once-de-oro:sfx:v1';
    let ctx = null;
    let master = null;
    let crowd = null;          // ambiente de estadio en curso
    let enabled = localStorage.getItem(KEY) !== 'off';

    function audio() {
        if (!ctx) {
            const AC = window.AudioContext || window.webkitAudioContext;
            if (!AC) return null;
            ctx = new AC();
            master = ctx.createGain();
            master.gain.value = 0.6;
            master.connect(ctx.destination);
        }
        // Los navegadores arrancan el audio suspendido hasta que el usuario interactúa
        if (ctx.state === 'suspended') ctx.resume();
        return ctx;
    }

    const now = () => audio().currentTime;

    /** Ruido blanco reutilizable (la base del público y del silbato). */
    let noiseBuf = null;
    function noise() {
        const c = audio();
        if (!noiseBuf) {
            noiseBuf = c.createBuffer(1, c.sampleRate * 2, c.sampleRate);
            const d = noiseBuf.getChannelData(0);
            for (let i = 0; i < d.length; i++) d[i] = Math.random() * 2 - 1;
        }
        const src = c.createBufferSource();
        src.buffer = noiseBuf;
        src.loop = true;
        return src;
    }

    function env(gain, t0, attack, hold, release, peak) {
        gain.gain.setValueAtTime(0.0001, t0);
        gain.gain.exponentialRampToValueAtTime(peak, t0 + attack);
        gain.gain.setValueAtTime(peak, t0 + attack + hold);
        gain.gain.exponentialRampToValueAtTime(0.0001, t0 + attack + hold + release);
    }

    // ---------------------------------------------------------------- SILBATO
    // Silbato de árbitro: dos tonos agudos batiendo entre sí + aire.
    function whistle(dur = 0.5, freq = 2300) {
        if (!enabled || !audio()) return;
        const t = now(), g = ctx.createGain();
        g.connect(master);
        env(g, t, 0.02, dur * 0.55, dur * 0.4, 0.35);

        [freq, freq * 1.28].forEach((f, i) => {
            const o = ctx.createOscillator();
            o.type = 'sine';
            o.frequency.setValueAtTime(f, t);
            // el trino característico de la bolita dentro del silbato
            const lfo = ctx.createOscillator(), lg = ctx.createGain();
            lfo.frequency.value = 22 + i * 4;
            lg.gain.value = f * 0.02;
            lfo.connect(lg).connect(o.frequency);
            lfo.start(t); lfo.stop(t + dur + 0.2);
            o.connect(g); o.start(t); o.stop(t + dur + 0.2);
        });

        const air = noise(), af = ctx.createBiquadFilter(), ag = ctx.createGain();
        af.type = 'bandpass'; af.frequency.value = freq; af.Q.value = 8;
        ag.gain.value = 0.05;
        air.connect(af).connect(ag).connect(g);
        air.start(t); air.stop(t + dur + 0.2);
    }

    // ---------------------------------------------------------------- PÚBLICO
    /** Murmullo continuo de estadio. */
    function crowdStart() {
        if (!enabled || !audio() || crowd) return;
        const t = now();
        const src = noise();
        const lp = ctx.createBiquadFilter(); lp.type = 'lowpass'; lp.frequency.value = 900;
        const hp = ctx.createBiquadFilter(); hp.type = 'highpass'; hp.frequency.value = 180;
        const g = ctx.createGain();
        g.gain.setValueAtTime(0.0001, t);
        g.gain.exponentialRampToValueAtTime(0.06, t + 1.5);

        // olas lentas: el público nunca suena parejo
        const lfo = ctx.createOscillator(), lg = ctx.createGain();
        lfo.type = 'sine'; lfo.frequency.value = 0.13; lg.gain.value = 0.025;
        lfo.connect(lg).connect(g.gain);
        lfo.start(t);

        src.connect(hp).connect(lp).connect(g).connect(master);
        src.start(t);
        crowd = { src, g, lfo };
    }

    function crowdStop() {
        if (!crowd) return;
        const t = now();
        crowd.g.gain.cancelScheduledValues(t);
        crowd.g.gain.setValueAtTime(crowd.g.gain.value, t);
        crowd.g.gain.exponentialRampToValueAtTime(0.0001, t + 1.2);
        crowd.src.stop(t + 1.4);
        crowd.lfo.stop(t + 1.4);
        crowd = null;
    }

    /** Estallido del público (gol, atajadón). */
    function roar(power = 1) {
        if (!enabled || !audio()) return;
        const t = now(), dur = 2.2 * power;
        const src = noise();
        const bp = ctx.createBiquadFilter(); bp.type = 'bandpass';
        bp.frequency.setValueAtTime(400, t);
        bp.frequency.exponentialRampToValueAtTime(1800, t + 0.35);
        bp.frequency.exponentialRampToValueAtTime(600, t + dur);
        bp.Q.value = 0.8;
        const g = ctx.createGain();
        env(g, t, 0.12, dur * 0.35, dur * 0.6, 0.5 * power);
        src.connect(bp).connect(g).connect(master);
        src.start(t); src.stop(t + dur + 0.3);
    }

    // ---------------------------------------------------------------- MUSICALES
    function tone(freq, t0, dur, type = 'sawtooth', peak = 0.18) {
        const o = ctx.createOscillator(), g = ctx.createGain();
        o.type = type; o.frequency.value = freq;
        env(g, t0, 0.03, dur * 0.5, dur * 0.5, peak);
        o.connect(g).connect(master);
        o.start(t0); o.stop(t0 + dur + 0.1);
    }

    /** Bocina de gol + estallido del público. */
    function goal() {
        if (!enabled || !audio()) return;
        const t = now();
        [220, 277, 330].forEach(f => tone(f, t, 1.1, 'sawtooth', 0.16));
        tone(440, t + 0.12, 0.9, 'square', 0.10);
        roar(1.2);
    }

    /** Fanfarria de campeón. */
    function victory() {
        if (!enabled || !audio()) return;
        const t = now();
        const notas = [523.25, 659.25, 783.99, 1046.5];   // Do mayor ascendente
        notas.forEach((f, i) => tone(f, t + i * 0.16, 0.7, 'sawtooth', 0.17));
        [523.25, 659.25, 783.99, 1046.5].forEach(f => tone(f, t + 0.72, 1.6, 'sawtooth', 0.12));
        roar(1.4);
    }

    /** Silencio de derrota: tonos que caen. */
    function defeat() {
        if (!enabled || !audio()) return;
        const t = now();
        [392, 349.23, 293.66].forEach((f, i) => tone(f, t + i * 0.26, 0.9, 'triangle', 0.14));
    }

    /** Tarjeta / falta: bip seco. */
    function card(red = false) {
        if (!enabled || !audio()) return;
        tone(red ? 180 : 320, now(), 0.16, 'square', 0.12);
    }

    /** Queja del público por una falta o un offside. */
    function boo() {
        if (!enabled || !audio()) return;
        const t = now(), src = noise();
        const bp = ctx.createBiquadFilter(); bp.type = 'bandpass';
        bp.frequency.setValueAtTime(500, t);
        bp.frequency.exponentialRampToValueAtTime(220, t + 1.0);
        bp.Q.value = 1.2;
        const g = ctx.createGain();
        env(g, t, 0.15, 0.35, 0.7, 0.22);
        src.connect(bp).connect(g).connect(master);
        src.start(t); src.stop(t + 1.4);
    }

    window.odoSfx = {
        play(name) {
            switch (name) {
                case 'whistle': whistle(0.45); break;
                case 'whistleLong': whistle(1.1, 2100); break;
                case 'goal': goal(); break;
                case 'victory': victory(); break;
                case 'defeat': defeat(); break;
                case 'yellow': card(false); break;
                case 'red': card(true); break;
                case 'boo': boo(); break;
                case 'roar': roar(1); break;
            }
        },
        crowdStart, crowdStop,
        isOn: () => enabled,
        toggle() {
            enabled = !enabled;
            localStorage.setItem(KEY, enabled ? 'on' : 'off');
            if (!enabled) crowdStop();
            return enabled;
        },
    };
})();
