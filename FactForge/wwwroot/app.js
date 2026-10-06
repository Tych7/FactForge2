const SlideType = { Text: 0, MultipleChoice: 1, OpenQuestion: 2, MusicQuestion: 3, Leaderboard: 4 };

const screens = {
  join: document.getElementById("screen-join"),
  waiting: document.getElementById("screen-waiting"),
  textSlide: document.getElementById("screen-textslide"),
  question: document.getElementById("screen-question"),
  musicQuestion: document.getElementById("screen-music-question"),
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
const revealIcon = document.getElementById("revealIcon");
const revealRank = document.getElementById("revealRank");
const finalLeaderboard = document.getElementById("finalLeaderboard");
const musicArtistInput = document.getElementById("musicArtist");
const musicTitleInput = document.getElementById("musicTitle");
const musicSubmitBtn = document.getElementById("musicSubmitBtn");
const musicTimerBar = document.getElementById("musicTimerBar");
const musicRevealResults = document.getElementById("musicRevealResults");
const musicArtistResult = document.getElementById("musicArtistResult");
const musicTitleResult = document.getElementById("musicTitleResult");

const params = new URLSearchParams(window.location.search);
if (params.get("code")) joinCodeInput.value = params.get("code").toUpperCase();

let connection = null;
let myName = "";
let selectedAnswer = null;
let countdownHandle = null;
let questionPoints = 0;
let currentSlideType = null;
let musicAnswerCorrect  = false;
let musicArtistCorrect = false;
let musicTitleCorrect = false;

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
  connection.on("QuestionPoints", (points) => {
    questionPoints = points ?? 0;

    if (!screens.reveal.classList.contains("hidden")) {
      revealRank.textContent = `+${questionPoints} points`;
    }
  });
}

let lastLeaderboard = [];

function onSlideStarted(slide) {
  selectedAnswer = null;
  questionPoints = 0;
  musicAnswerCorrect = false;
  musicArtistCorrect = false;
  musicTitleCorrect = false;
  stopCountdown();
  currentSlideType = slide.type;

  if (slide.type === SlideType.Text ||
      slide.type === SlideType.Leaderboard) {
    showScreen("textSlide");
    return;
  }

  // Music question
  if (slide.type === SlideType.MusicQuestion) {
    musicArtistInput.value = "";
    musicTitleInput.value = "";

    musicArtistInput.disabled = false;
    musicTitleInput.disabled = false;
    musicSubmitBtn.disabled = false;

    musicTimerBar.style.width = "100%";

    showScreen("musicQuestion");

    if (slide.deadlineUtc) {
      startMusicCountdown(slide.timeSeconds);
    }

    return;
  }

  // Normal question
  questionText.textContent = slide.question || "";
  choicesEl.innerHTML = "";

  if (slide.type === SlideType.MultipleChoice) {
    choicesEl.classList.remove("single-column");

    (slide.options || []).forEach((option) => {
      const btn = document.createElement("button");
      btn.type = "button";
      btn.className = "choice-btn";
      btn.textContent = option;

      btn.addEventListener("click", () =>
        submitAnswer(option, btn)
      );

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

      if (input.value.trim())
        submitAnswer(input.value.trim(), submit);
    });

    choicesEl.appendChild(form);
  }

  showScreen("question");

  if (slide.deadlineUtc) {
    startTimerCountdown(slide.timeSeconds);
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

async function submitMusicAnswer() {
  if (selectedAnswer !== null) return;

  const artist = musicArtistInput.value.trim();
  const title = musicTitleInput.value.trim();

  if (!artist || !title) return;

  selectedAnswer = `${artist}|${title}`;

  musicArtistInput.disabled = true;
  musicTitleInput.disabled = true;
  musicSubmitBtn.disabled = true;

  try {
    const result = await connection.invoke(
      "SubmitMusicAnswer",
      artist,
      title
    );

    musicAnswerCorrect = result.isCorrect;
    musicArtistCorrect = result.artistCorrect;
    musicTitleCorrect = result.titleCorrect;
    questionPoints = result.pointsAwarded ?? 0;

    stopCountdown();
    showScreen("locked");
  } catch (err) {
    console.error(err);

    selectedAnswer = null;
    musicArtistInput.disabled = false;
    musicTitleInput.disabled = false;
    musicSubmitBtn.disabled = false;
  }
}

musicSubmitBtn.addEventListener("click", submitMusicAnswer);

function startTimerCountdown(totalSeconds) {
  startCountdown(totalSeconds, timerBar);
}

function startMusicCountdown(totalSeconds) {
  startCountdown(totalSeconds, musicTimerBar);
}

function startCountdown(totalSeconds, progressBar) {
  stopCountdown();

  const totalMs = totalSeconds * 1000;
  const startedAt = Date.now();

  const update = () => {
    const remainingMs = Math.max(
      0,
      totalMs - (Date.now() - startedAt)
    );

    const pct = Math.max(
      0,
      Math.min(100, (remainingMs / totalMs) * 100)
    );

    progressBar.style.width = `${pct}%`;

    if (remainingMs <= 0) {
      stopCountdown();
    }
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

  revealIcon.className = "reveal-icon";
  revealIcon.classList.remove("hidden");

  musicRevealResults.classList.add("hidden");

  // Music question
  if (currentSlideType === SlideType.MusicQuestion) {
    // Hide the big correct/incorrect icon.
    revealIcon.classList.add("hidden");

    musicRevealResults.classList.remove("hidden");

    musicArtistResult.innerHTML = `
      <span class="music-result-icon ${musicArtistCorrect ? "correct" : "incorrect"}">
        ${musicArtistCorrect ? "✓" : "✕"}
      </span>
      <span>Artist</span>
    `;

    musicTitleResult.innerHTML = `
      <span class="music-result-icon ${musicTitleCorrect ? "correct" : "incorrect"}">
        ${musicTitleCorrect ? "✓" : "✕"}
      </span>
      <span>Title</span>
    `;

    revealRank.textContent = `+${questionPoints} points`;

    showScreen("reveal");
    return;
  }

  // Normal question
  const correctAnswer = reveal.correctAnswer;

  const wasCorrect =
    selectedAnswer !== null &&
    correctAnswer !== null &&
    selectedAnswer.trim().toLowerCase() ===
      String(correctAnswer).trim().toLowerCase();

  if (wasCorrect) {
    revealIcon.classList.add("correct");
    revealIcon.textContent = "✓";
  } else {
    revealIcon.classList.add("incorrect");
    revealIcon.textContent = "✕";
  }

  revealRank.textContent = `+${questionPoints} points`;

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
