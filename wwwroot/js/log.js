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

    // Keyboard shortcuts for logging from video on a laptop.
    // Digits pick a player by shirt number; letters press the button with that data-key.
    const players = [...form.querySelectorAll('input[name="playerId"]')];
    let digits = "";
    let digitTimer;
    let passPending = false;
    let passTimer;

    function selectPlayer(radio) {
        radio.checked = true;
        radio.dispatchEvent(new Event("change", { bubbles: true }));
    }

    function markPending(number) {
        form.querySelectorAll(".tile.key-pending").forEach(t => t.classList.remove("key-pending"));
        const match = players.find(p => p.dataset.number === number);
        if (match) form.querySelector(`label[for="${match.id}"]`).classList.add("key-pending");
    }

    function finishDigits() {
        clearTimeout(digitTimer);
        const match = players.find(p => p.dataset.number === digits);
        if (match) {
            selectPlayer(match);
            hideStatus();
        } else {
            showStatus(`No player wearing #${digits}.`, "warn");
        }
        digits = "";
        markPending("");
    }

    function press(key) {
        const button = form.querySelector(`button[data-key="${key}"]`);
        if (!button) return false;
        if (button.disabled) {
            if (button.dataset.needsPlayer === "true" && !selectedPlayer()) {
                showStatus("Pick a player first: type their shirt number.", "warn");
            }
            return true;
        }
        form.requestSubmit(button);
        return true;
    }

    document.addEventListener("keydown", event => {
        if (event.altKey || event.metaKey) return;
        if (event.ctrlKey && event.key.toLowerCase() !== "z") return;
        if (event.target.closest("input[type=text], input[type=number], textarea, select")) return;

        const key = event.key.toLowerCase();

        if (passPending && /^[0-3]$/.test(key)) {
            passPending = false;
            clearTimeout(passTimer);
            press(`r${key}`);
            event.preventDefault();
            return;
        }

        if (/^[0-9]$/.test(key)) {
            digits += key;
            clearTimeout(digitTimer);
            const longer = players.some(p => p.dataset.number.startsWith(digits) && p.dataset.number !== digits);
            if (longer) {
                // e.g. "1" could still become "17": wait briefly for a second digit.
                markPending(digits);
                digitTimer = setTimeout(finishDigits, 600);
            } else {
                finishDigits();
            }
            event.preventDefault();
            return;
        }

        if (key === "r") {
            passPending = true;
            clearTimeout(passTimer);
            passTimer = setTimeout(() => { passPending = false; hideStatus(); }, 2000);
            showStatus("Pass: press 0, 1, 2 or 3.", "pending");
            event.preventDefault();
            return;
        }

        if (key === "escape") {
            passPending = false;
            const checked = selectedPlayer();
            if (checked) {
                checked.checked = false;
                const url = new URL(location.href);
                url.searchParams.delete("playerId");
                history.replaceState(null, "", url);
                refreshButtons();
            }
            hideStatus();
            return;
        }

        if (key === "backspace" || (event.ctrlKey && key === "z")) {
            if (press("backspace")) event.preventDefault();
            return;
        }

        if (key.length === 1 && press(key)) {
            event.preventDefault();
        }
    });

    refreshButtons();
})();
