import { elements, showScreen } from "./screens.js";
import { createConnection } from "./connection.js";
import {
  setConnection,
  initializeQuiz,
  quizHandlers,
} from "./quiz.js";
import { showPlayerAvatar } from "./playerAvatar.js";

const {
  joinCodeInput,
  playerNameInput,
  joinBtn,
  joinError,
} = elements;

const params = new URLSearchParams(window.location.search);
const codeFromUrl = params.get("code");

console.log("Current URL:", window.location.href);
console.log("Code from QR URL:", codeFromUrl);
console.log("Join code input:", joinCodeInput);

if (codeFromUrl && joinCodeInput) {
  joinCodeInput.value = codeFromUrl.toUpperCase();
  console.log("Input value after setting:", joinCodeInput.value);
}

joinBtn.addEventListener("click", joinGame);

initializeQuiz();

async function joinGame() {
  const code = joinCodeInput.value.trim().toUpperCase();
  const name = playerNameInput.value.trim();

  joinError.textContent = "";

  if (!code || !name) {
    joinError.textContent = "Enter a game code and your name.";
    return;
  }

  joinBtn.disabled = true;

  let connection;

  try {
    connection = createConnection(quizHandlers);
    setConnection(connection);

    await connection.start();

    const player = await connection.invoke(
      "JoinSession",
      code,
      name
    );

    if (!player) {
      joinError.textContent = "That game code wasn't found.";

      await connection.stop();
      setConnection(null);
      joinBtn.disabled = false;
      return;
    }

    showPlayerAvatar(name);
    showScreen("waiting");
  } catch (error) {
    console.error(error);

    joinError.textContent =
      "Couldn't connect. Check the code and try again.";

    if (connection) {
      try {
        await connection.stop();
      } catch (stopError) {
        console.error(stopError);
      }
    }

    setConnection(null);
    joinBtn.disabled = false;
  }
}