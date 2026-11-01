(() => {
    const reducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;

    document.querySelectorAll("[data-reveal]").forEach(button => {
        const input = document.getElementById(button.dataset.reveal);
        if (!input) return;

        button.addEventListener("click", () => {
            const hidden = input.type === "password";
            input.type = hidden ? "text" : "password";
            button.classList.toggle("on", hidden);
            button.setAttribute("aria-label", hidden ? "Hide password" : "Show password");
            input.focus();
        });
    });

    const labels = ["Too short", "Weak", "Okay", "Strong", "Excellent"];

    document.querySelectorAll("[data-strength]").forEach(input => {
        const meter = document.getElementById(input.dataset.strength);
        if (!meter) return;

        const min = Number(meter.dataset.min || 8);
        const label = meter.querySelector(".strength-label");

        const update = () => {
            const value = input.value;
            let score = 0;

            if (value.length >= min) {
                score = 1;
                if (value.length >= min + 4) score++;
                if (/[a-z]/.test(value) && /[A-Z]/.test(value)) score++;
                if (/\d/.test(value) && /[^A-Za-z0-9]/.test(value)) score++;
            }

            meter.dataset.score = value.length === 0 ? "" : String(score);
            if (label) label.textContent = value.length === 0 ? `Use at least ${min} characters.` : labels[score];
        };

        input.addEventListener("input", update);
        update();
    });

    document.querySelectorAll("[data-match]").forEach(input => {
        const other = document.getElementById(input.dataset.match);
        const wrap = input.closest(".input-wrap");
        if (!other || !wrap) return;

        const update = () => wrap.classList.toggle("matched", input.value.length > 0 && input.value === other.value);
        input.addEventListener("input", update);
        other.addEventListener("input", update);
    });

    document.querySelectorAll("[data-count]").forEach(element => {
        const target = Number(element.dataset.count);
        if (reducedMotion || !Number.isFinite(target) || target === 0) return;

        const start = performance.now();
        const duration = 1200;
        const step = now => {
            const t = Math.min((now - start) / duration, 1);
            element.textContent = Math.round(target * (1 - Math.pow(1 - t, 3))).toLocaleString("en-GB");
            if (t < 1) requestAnimationFrame(step);
        };
        element.textContent = "0";
        requestAnimationFrame(step);
    });

    document.querySelectorAll(".auth-card, .panel, .profile-card, .step-card").forEach(card => {
        card.addEventListener("pointermove", event => {
            const rect = card.getBoundingClientRect();
            card.style.setProperty("--px", `${event.clientX - rect.left}px`);
            card.style.setProperty("--py", `${event.clientY - rect.top}px`);
        });
    });

    document.querySelectorAll("[data-mottos]").forEach(motto => {
        const text = motto.querySelector(".motto-text");
        let lines;

        try {
            lines = JSON.parse(motto.dataset.mottos);
        } catch {
            return;
        }

        if (!text || !Array.isArray(lines) || lines.length < 2) return;

        for (let i = lines.length - 1; i > 0; i--) {
            const j = Math.floor(Math.random() * (i + 1));
            [lines[i], lines[j]] = [lines[j], lines[i]];
        }

        let index = Math.max(0, lines.indexOf(text.textContent.trim()));

        setInterval(() => {
            index = (index + 1) % lines.length;

            if (reducedMotion) {
                text.textContent = lines[index];
                return;
            }

            text.classList.add("leaving");
            setTimeout(() => {
                text.textContent = lines[index];
                text.classList.remove("leaving");
            }, 350);
        }, 6000);
    });

    if (reducedMotion) return;

    const layers = [...document.querySelectorAll(".parallax")];
    let frame = 0;

    window.addEventListener("pointermove", event => {
        if (frame) return;
        frame = requestAnimationFrame(() => {
            frame = 0;
            const x = event.clientX / window.innerWidth - 0.5;
            const y = event.clientY / window.innerHeight - 0.5;

            layers.forEach(layer => {
                const depth = Number(layer.dataset.depth || 1);
                layer.style.transform = `translate3d(${(-x * 30 * depth).toFixed(1)}px, ${(-y * 20 * depth).toFixed(1)}px, 0)`;
            });
        });
    });
})();
