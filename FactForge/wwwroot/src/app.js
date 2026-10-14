import { elements, showScreen } from "./screens.js";
import { createConnection } from "./connection.js";
import {
  setConnection,
  initializeQuiz,
  quizHandlers,
  restoreRejoinedSession,
} from "./quiz.js";
import { showPlayerAvatar } from "./playerAvatar.js";

const {
  joinCodeInput,
  playerNameInput,
  joinBtn,
  joinError,
  rejoinOptions,
  rejoinPlayers,
  joinAsNewBtn,
} = elements;

const params = new URLSearchParams(window.location.search);
const codeFromUrl = params.get("code");

if (codeFromUrl && joinCodeInput) {
  joinCodeInput.value = codeFromUrl.toUpperCase();
}

let connection = null;
let currentCode = "";

joinBtn.addEventListener("click", joinGame);
joinAsNewBtn.addEventListener("click", joinAsNewPlayer);

let lookupTimeout = null;

joinCodeInput.addEventListener("input", () => {
  const code = joinCodeInput.value.trim().toUpperCase();

  // Keep the code uppercase.
  joinCodeInput.value = code;

  clearTimeout(lookupTimeout);

  // Only look up players when the code has 5 characters.
  if (code.length !== 5) {
    rejoinOptions.classList.add("hidden");
    joinAsNewBtn.classList.add("hidden");
    joinBtn.classList.remove("hidden");
    return;
  }

  lookupTimeout = setTimeout(() => {
    checkForRejoinOptions(code);
  }, 300);
});

initializeQuiz();

// Also check a game code supplied through the URL.
if (codeFromUrl && codeFromUrl.length === 5) {
  checkForRejoinOptions(codeFromUrl.toUpperCase());
}

async function joinGame() {
  currentCode = joinCodeInput.value.trim().toUpperCase();
  joinError.textContent = "";

  if (!currentCode) {
    joinError.textContent = "Enter a game code.";
    return;
  }

  joinBtn.disabled = true;

  try {
    await ensureConnection();

    const disconnectedPlayers = await connection.invoke(
      "GetDisconnectedPlayers",
      currentCode
    );

    if (disconnectedPlayers.length > 0) {
      renderRejoinOptions(disconnectedPlayers);
      rejoinOptions.classList.remove("hidden");
      joinBtn.classList.add("hidden");
      joinAsNewBtn.classList.remove("hidden");
      return;
    }

    await joinAsNewPlayer();
  } catch (error) {
    console.error(error);
    joinError.textContent =
      "Couldn't connect or find that game. Check the code and try again.";

    await closeConnection();
    joinBtn.disabled = false;
  }
}

function renderRejoinOptions(players) {
  rejoinPlayers.replaceChildren();

  for (const player of players) {
    const button = document.createElement("button");
    button.type = "button";
    button.className = "rejoin-player-btn";

    const name = document.createElement("span");
    name.textContent = player.name;

    const score = document.createElement("span");
    score.className = "rejoin-player-score";
    score.textContent = `${player.score} points`;

    button.append(name, score);

    button.addEventListener("click", async () => {
      await rejoinPlayer(player.id);
    });

    rejoinPlayers.appendChild(button);
  }
}

async function checkForRejoinOptions(code) {
  if (connection) return;

  let lookupConnection = null;

  try {
    lookupConnection = createConnection(quizHandlers);
    await lookupConnection.start();

    const players = await lookupConnection.invoke(
      "GetDisconnectedPlayers",
      code
    );

    if (joinCodeInput.value.trim().toUpperCase() !== code) {
      return;
    }

    currentCode = code;

    if (players.length > 0) {
      renderRejoinOptions(players);
      rejoinOptions.classList.remove("hidden");
      joinBtn.classList.add("hidden");
      joinAsNewBtn.classList.remove("hidden");
    } else {
      rejoinOptions.classList.add("hidden");
      joinAsNewBtn.classList.add("hidden");
      joinBtn.classList.remove("hidden");
    }
  } catch (error) {
    console.error("Couldn't check for previous players:", error);
  } finally {
    if (lookupConnection) {
      try {
        await lookupConnection.stop();
      } catch (error) {
        console.error(error);
      }
    }
  }
}

async function ensureConnection() {
  if (connection?.state === signalR.HubConnectionState.Connected) {
    return;
  }

  connection = createConnection(quizHandlers);
  setConnection(connection);

  await connection.start();
}

async function rejoinPlayer(playerId) {
  disableJoinOptions(true);
  joinError.textContent = "";

  try {
    await ensureConnection();

    const state = await connection.invoke(
      "RejoinSession",
      currentCode,
      playerId
    );

    if (!state) {
      joinError.textContent =
        "That player is no longer available. Try again.";

      await refreshDisconnectedPlayers();
      return;
    }

    showPlayerAvatar(state.player.name);
    rejoinOptions.classList.add("hidden");
    joinBtn.classList.remove("hidden");

    restoreRejoinedSession(state);
  } catch (error) {
    console.error(error);
    joinError.textContent = "Couldn't rejoin. Please try again.";
  } finally {
    disableJoinOptions(false);
  }
}

async function joinAsNewPlayer() {
  const name = playerNameInput.value.trim();

  if (!name) {
    joinError.textContent = "Enter your name to join as a new player.";
    return;
  }

  disableJoinOptions(true);
  joinError.textContent = "";

  try {
    await ensureConnection();

    const player = await connection.invoke(
      "JoinSession",
      currentCode,
      name
    );

    if (!player) {
      joinError.textContent = "That game code wasn't found.";
      return;
    }

    showPlayerAvatar(player.name);
    rejoinOptions.classList.add("hidden");
    showScreen("waiting");
  } catch (error) {
    console.error(error);
    joinError.textContent = "Couldn't join the game. Please try again.";
  } finally {
    disableJoinOptions(false);
  }
}

async function refreshDisconnectedPlayers() {
  const players = await connection.invoke(
    "GetDisconnectedPlayers",
    currentCode
  );

  renderRejoinOptions(players);
}

function disableJoinOptions(disabled) {
  joinBtn.disabled = disabled;
  joinAsNewBtn.disabled = disabled;

  rejoinPlayers
    .querySelectorAll("button")
    .forEach((button) => {
      button.disabled = disabled;
    });
}

async function closeConnection() {
  if (!connection) return;

  try {
    await connection.stop();
  } catch (error) {
    console.error(error);
  }

  connection = null;
  setConnection(null);
}