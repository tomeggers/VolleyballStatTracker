// Live logging on the Log page: taps are sent in the background and the page updates in place.
// Without this script the same form still works as a normal post and reload.
(() => {
    const form = document.getElementById("log-form");
    if (!form) return;

    const byId = id => document.getElementById(id);
    const reduceMotion = matchMedia("(prefers-reduced-motion: reduce)").matches;
    let setOver = form.dataset.setOver === "true";
    let hasEntries = !byId("undo").disabled;
    let queue = Promise.resolve();

    const selectedPlayer = () => form.querySelector('input[name="playerId"]:checked');

    function refreshButtons() {
        const hasPlayer = selectedPlayer() !== null;
        form.querySelectorAll("button[data-scores]").forEach(button => {
            const locked = setOver && button.dataset.scores === "true";
            const needsPlayer = button.dataset.needsPlayer === "true" && !hasPlayer;
            button.disabled = locked || needsPlayer || button.dataset.guard === "on";
            button.classList.toggle("locked", locked);
        });
        const undo = byId("undo");
        undo.disabled = !hasEntries || undo.dataset.guard === "on";

        const player = selectedPlayer();
        byId("acting").textContent = player ? player.dataset.label : "Pick a player first";
        byId("acting").classList.toggle("empty", !player);
    }

    function pulse(element, className, ms) {
        if (reduceMotion && className === "bump") return;
        element.classList.remove(className);
        void element.offsetWidth; // restart the animation if it is already running
        element.classList.add(className);
        setTimeout(() => element.classList.remove(className), ms);
    }

    function setScore(element, value) {
        if (element.textContent !== String(value)) {
            element.textContent = value;
            pulse(element, "bump", 200);
        }
    }

    let statusTimer;
    function showStatus(text, kind) {
        clearTimeout(statusTimer);
        const status = byId("status");
        status.textContent = text;
        status.dataset.kind = kind;
        status.hidden = false;
    }
    function hideStatus() {
        clearTimeout(statusTimer);
        byId("status").hidden = true;
    }

    function apply(state) {
        setScore(byId("score-us"), state.us);
        setScore(byId("score-them"), state.them);
        byId("sets-score").textContent = `${state.setsWon}–${state.setsLost}`;

        const tab = document.querySelector(".set-tab.active");
        if (tab) {
            tab.querySelector("small").textContent = `${state.us}–${state.them}`;
            tab.classList.toggle("won", state.setResult === "Won");
            tab.classList.toggle("lost", state.setResult === "Lost");
        }

        byId("last-entry").textContent = state.lastEntry ?? "No entries yet";
        byId("last-entry-bar").dataset.effect = state.lastEntryEffect;
        hasEntries = state.lastEntry !== null;

        setOver = state.setResult !== "InProgress";
        byId("set-over").hidden = !setOver;
        byId("set-over-result").textContent = state.setResult === "Won" ? "won" : "lost";

        const nextSet = byId("next-set");
        if (nextSet) nextSet.hidden = !state.canAddSet;

        refreshButtons();
    }

    async function send(url, body, isUndo) {
        // Only show "Saving" if the server is slow, so normal taps don't flicker.
        statusTimer = setTimeout(() => showStatus("Saving…", "pending"), 400);
        try {
            const response = await fetch(url, {
                method: "POST",
                body,
                headers: { Accept: "application/json" }
            });
            if (!response.ok) throw new Error(`HTTP ${response.status}`);

            const state = await response.json();
            apply(state);
            if (state.accepted) {
                hideStatus();
            } else {
                showStatus(isUndo ? "Nothing to undo." : "Not logged: this set is decided. Undo to correct the score.", "warn");
            }
        } catch {
            showStatus("Not saved. Check the connection and tap again.", "error");
        }
    }

    form.addEventListener("submit", event => {
        const button = event.submitter;
        if (!button || !window.fetch) return; // fall back to a normal post

        event.preventDefault();
        if (button.dataset.guard === "on") return;

        // Block an accidental double tap on the same button for 250 ms.
        button.dataset.guard = "on";
        button.disabled = true;
        setTimeout(() => {
            delete button.dataset.guard;
            refreshButtons();
        }, 250);

        pulse(button, "flash", 150);
        if (navigator.vibrate) navigator.vibrate(button.dataset.scores === "true" ? [15, 40, 15] : 15);

        const body = new FormData(form);
        const isUndo = button.id === "undo";
        queue = queue.then(() => send(button.formAction, body, isUndo));
    });

    // Remember the selected player in the address bar, so a refresh or set tab keeps them selected.
    form.addEventListener("change", event => {
        if (event.target.name !== "playerId") return;
        const url = new URL(location.href);
        url.searchParams.set("playerId", event.target.value);
        history.replaceState(null, "", url);
        document.querySelectorAll(".set-tab").forEach(tab => {
            const href = new URL(tab.href);
            href.searchParams.set("playerId", event.target.value);
            tab.href = href;
        });
        refreshButtons();
    });

    refreshButtons();
})();
