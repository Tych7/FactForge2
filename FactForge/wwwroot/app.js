const SlideType = { Text: 0, MultipleChoice: 1, OpenQuestion: 2 };

const screens = {
  join: document.getElementById("screen-join"),
  waiting: document.getElementById("screen-waiting"),
  textSlide: document.getElementById("screen-textslide"),
  question: document.getElementById("screen-question"),
  locked: document.getElementById("screen-locked"),
  reveal: document.getElementById("screen-reveal"),
  ended: document.getElementById("screen-ended"),
};

function showScreen(name) {
  for (const key in screens) screens[key].classList.toggle("hidden", key !== name);
}

const joinCodeInput = document.getElementById("joinCode");
const playerNameInput = document.getElementById("playerName");
const joinBtn = document.getElementById("joinBtn");
const joinError = document.getElementById("joinError");
const questionText = document.getElementById("questionText");
const choicesEl = document.getElementById("choices");
const timerBar = document.getElementById("timerBar");
const revealTitle = document.getElementById("revealTitle");
const revealAnswer = document.getElementById("revealAnswer");
const revealRank = document.getElementById("revealRank");
const finalLeaderboard = document.getElementById("finalLeaderboard");

const params = new URLSearchParams(window.location.search);
if (params.get("code")) joinCodeInput.value = params.get("code").toUpperCase();

let connection = null;
let myName = "";
let selectedAnswer = null;
let countdownHandle = null;

joinBtn.addEventListener("click", joinGame);

async function joinGame() {
  const code = joinCodeInput.value.trim().toUpperCase();
  const name = playerNameInput.value.trim();
  joinError.textContent = "";

  if (!code || !name) {
    joinError.textContent = "Enter a game code and your name.";
    return;
  }

  joinBtn.disabled = true;
  try {
    connection = new signalR.HubConnectionBuilder()
      .withUrl("/quizhub")
      .withAutomaticReconnect()
      .build();

    registerHandlers();
    await connection.start();

    const player = await connection.invoke("JoinSession", code, name);
    if (!player) {
      joinError.textContent = "That game code wasn't found.";
      await connection.stop();
      joinBtn.disabled = false;
      return;
    }

    myName = player.name;
    showScreen("waiting");
  } catch (err) {
    console.error(err);
    joinError.textContent = "Couldn't connect. Check the code and try again.";
    joinBtn.disabled = false;
  }
}

function registerHandlers() {
  connection.on("SlideStarted", onSlideStarted);
  connection.on("SlideRevealed", onSlideRevealed);
  connection.on("LeaderboardUpdated", onLeaderboardUpdated);
  connection.on("SessionEnded", onSessionEnded);
}

let lastLeaderboard = [];

function onSlideStarted(slide) {
  selectedAnswer = null;
  stopCountdown();

  if (slide.type === SlideType.Text) {
    showScreen("textSlide");
    return;
  }

  questionText.textContent = slide.question || "";
  choicesEl.innerHTML = "";

  if (slide.type === SlideType.MultipleChoice) {
    choicesEl.classList.remove("single-column");
    (slide.options || []).forEach((option) => {
      const btn = document.createElement("button");
      btn.type = "button";
      btn.className = "choice-btn";
      btn.textContent = option;
      btn.addEventListener("click", () => submitAnswer(option, btn));
      choicesEl.appendChild(btn);
    });
  } else {
    choicesEl.classList.add("single-column");
    const form = document.createElement("form");
    form.className = "open-answer-form";
    const input = document.createElement("input");
    input.type = "text";
    input.placeholder = "Type your answer";
    const submit = document.createElement("button");
    submit.type = "submit";
    submit.textContent = "Submit";
    form.appendChild(input);
    form.appendChild(submit);
    form.addEventListener("submit", (e) => {
      e.preventDefault();
      if (input.value.trim()) submitAnswer(input.value.trim(), submit);
    });
    choicesEl.appendChild(form);
  }

  showScreen("question");

  if (slide.deadlineUtc) {
    startCountdown(slide.timeSeconds);
  }
}

async function submitAnswer(text, sourceEl) {
  if (selectedAnswer !== null) return;
  selectedAnswer = text;

  Array.from(choicesEl.querySelectorAll("button")).forEach((b) => (b.disabled = true));
  Array.from(choicesEl.querySelectorAll("input")).forEach((i) => (i.disabled = true));
  if (sourceEl) sourceEl.classList.add("selected");

  try {
    await connection.invoke("SubmitAnswer", text);
  } catch (err) {
    console.error(err);
  }
  stopCountdown();
  showScreen("locked");
}

function startCountdown(totalSeconds) {
  // Count down against the phone's own clock, started the moment the question appears,
  // rather than comparing the server's absolute deadline to the phone's clock: those two
  // clocks aren't guaranteed to be in sync, which threw the bar off by however much they drifted.
  const totalMs = totalSeconds * 1000;
  const startedAt = Date.now();
  const update = () => {
    const remainingMs = totalMs - (Date.now() - startedAt);
    const pct = Math.max(0, Math.min(100, (remainingMs / totalMs) * 100));
    timerBar.style.width = pct + "%";
    if (remainingMs <= 0) stopCountdown();
  };
  update();
  countdownHandle = setInterval(update, 200);
}

function stopCountdown() {
  if (countdownHandle) {
    clearInterval(countdownHandle);
    countdownHandle = null;
  }
}

function onSlideRevealed(reveal) {
  stopCountdown();
  const correctAnswer = reveal.correctAnswer;
  const wasCorrect =
    selectedAnswer !== null &&
    correctAnswer !== null &&
    selectedAnswer.trim().toLowerCase() === String(correctAnswer).trim().toLowerCase();

  if (selectedAnswer === null) {
    revealTitle.textContent = "Time's up!";
    revealTitle.className = "";
  } else if (wasCorrect) {
    revealTitle.textContent = "Correct! 🎉";
    revealTitle.className = "reveal-correct";
  } else {
    revealTitle.textContent = "Not quite";
    revealTitle.className = "reveal-incorrect";
  }

  revealAnswer.textContent = correctAnswer ? `Correct answer: ${correctAnswer}` : "";

  const mine = lastLeaderboard.find((p) => p.name === myName);
  revealRank.textContent = mine ? `Your score: ${mine.score}` : "";

  showScreen("reveal");
}

function onLeaderboardUpdated(leaderboard) {
  lastLeaderboard = leaderboard || [];
}

function onSessionEnded(leaderboard) {
  stopCountdown();
  finalLeaderboard.innerHTML = "";
  (leaderboard || []).forEach((entry) => {
    const li = document.createElement("li");
    li.textContent = `${entry.name} — ${entry.score}`;
    finalLeaderboard.appendChild(li);
  });
  showScreen("ended");
}
