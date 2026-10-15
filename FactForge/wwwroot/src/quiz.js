import { SlideType, screens, elements, showScreen } from "./screens.js";
import {
  startTimerCountdown,
  startMusicCountdown,
  stopCountdown,
} from "./countdown.js";

let connection = null;
let selectedAnswer = null;
let questionPoints = 0;
let myAnswerCorrect = false;
let currentSlideType = null;

let musicAnswerCorrect = false;
let musicArtistCorrect = false;
let musicTitleCorrect = false;

export function setConnection(value) {
  connection = value;
}

function resetQuestionState() {
  selectedAnswer = null;
  questionPoints = 0;
  myAnswerCorrect = false;

  musicAnswerCorrect = false;
  musicArtistCorrect = false;
  musicTitleCorrect = false;

  stopCountdown();
}

export function onSlideStarted(slide) {
  resetQuestionState();
  currentSlideType = slide.type;

  if (
    slide.type === SlideType.Text ||
    slide.type === SlideType.Leaderboard
  ) {
    showScreen("textSlide");
    return;
  }

  if (slide.type === SlideType.MusicQuestion) {
    startMusicQuestion(slide);
    return;
  }

  startNormalQuestion(slide);
}

function startMusicQuestion(slide) {
  const {
    musicArtistInput,
    musicTitleInput,
    musicSubmitBtn,
    musicTimerBar,
  } = elements;

  musicArtistInput.value = "";
  musicTitleInput.value = "";

  musicArtistInput.disabled = false;
  musicTitleInput.disabled = false;
  musicSubmitBtn.disabled = false;

  musicTimerBar.style.width = "100%";

  showScreen("musicQuestion");

  if (slide.deadlineUtc && slide.timeSeconds > 0) {
    startMusicCountdown(getRemainingSeconds(slide), musicTimerBar);
  }
}

function startNormalQuestion(slide) {
  const { questionText, choices, timerBar } = elements;

  questionText.textContent = slide.question || "";
  choices.innerHTML = "";

  if (slide.type === SlideType.MultipleChoice) {
    choices.classList.remove("single-column");

    (slide.options || []).forEach((option) => {
      const button = document.createElement("button");

      button.type = "button";
      button.className = "choice-btn";
      button.textContent = option;

      button.addEventListener("click", () => {
        submitAnswer(option, button);
      });

      choices.appendChild(button);
    });
  } else {
    choices.classList.add("single-column");

    const form = document.createElement("form");
    form.className = "open-answer-form";

    const input = document.createElement("input");
    input.type = "text";
    input.placeholder = "Type your answer";

    const submit = document.createElement("button");
    submit.type = "submit";
    submit.textContent = "Submit";

    form.append(input, submit);

    form.addEventListener("submit", (event) => {
      event.preventDefault();

      const answer = input.value.trim();

      if (answer) {
        submitAnswer(answer, submit);
      }
    });

    choices.appendChild(form);
  }

  showScreen("question");

  if (slide.deadlineUtc && slide.timeSeconds > 0) {
    startTimerCountdown(getRemainingSeconds(slide), timerBar);
  }
}

async function submitAnswer(text, sourceElement) {
  if (selectedAnswer !== null) return;

  selectedAnswer = text;

  elements.choices
    .querySelectorAll("button")
    .forEach((button) => {
      button.disabled = true;
    });

  elements.choices
    .querySelectorAll("input")
    .forEach((input) => {
      input.disabled = true;
    });

  if (sourceElement) {
    sourceElement.classList.add("selected");
  }

  try {
    const result = await connection.invoke("SubmitAnswer", text);

    if (result) {
      myAnswerCorrect = result.isCorrect;
      questionPoints = result.pointsAwarded ?? 0;
    }
  } catch (error) {
    console.error(error);
  }

  stopCountdown();
  showScreen("locked");
}

async function submitMusicAnswer() {
  if (selectedAnswer !== null) return;

  const artist = elements.musicArtistInput.value.trim();
  const title = elements.musicTitleInput.value.trim();

  if (!artist || !title) return;

  selectedAnswer = `${artist}|${title}`;

  elements.musicArtistInput.disabled = true;
  elements.musicTitleInput.disabled = true;
  elements.musicSubmitBtn.disabled = true;

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
  } catch (error) {
    console.error(error);

    selectedAnswer = null;

    elements.musicArtistInput.disabled = false;
    elements.musicTitleInput.disabled = false;
    elements.musicSubmitBtn.disabled = false;
  }
}

export function onSlideRevealed() {
  stopCountdown();

  const {
    revealIcon,
    revealRank,
    musicRevealResults,
    musicArtistResult,
    musicTitleResult,
  } = elements;

  revealIcon.className = "reveal-icon";
  revealIcon.classList.remove("hidden");

  musicRevealResults.classList.add("hidden");

  if (currentSlideType === SlideType.MusicQuestion) {
    revealIcon.classList.add("hidden");
    musicRevealResults.classList.remove("hidden");

    renderMusicResult(musicArtistResult, "Artist", musicArtistCorrect);
    renderMusicResult(musicTitleResult, "Title", musicTitleCorrect);

    revealRank.textContent = `+${questionPoints} points`;

    showScreen("reveal");
    return;
  }

  if (myAnswerCorrect) {
    revealIcon.classList.add("correct");
    revealIcon.textContent = "✓";
  } else {
    revealIcon.classList.add("incorrect");
    revealIcon.textContent = "✕";
  }

  revealRank.textContent = `+${questionPoints} points`;

  showScreen("reveal");
}

function renderMusicResult(element, label, isCorrect) {
  const icon = document.createElement("span");
  icon.className = `music-result-icon ${
    isCorrect ? "correct" : "incorrect"
  }`;
  icon.textContent = isCorrect ? "✓" : "✕";

  const text = document.createElement("span");
  text.textContent = label;

  element.replaceChildren(icon, text);
}

export function onQuestionPoints(points) {
  questionPoints = points ?? 0;

  if (!screens.reveal.classList.contains("hidden")) {
    elements.revealRank.textContent = `+${questionPoints} points`;
  }
}

export function onLeaderboardUpdated() {
  // Retained as an event handler for future leaderboard UI updates.
}

export function onSessionEnded(leaderboard) {
  stopCountdown();

  const list = elements.finalLeaderboard;
  list.innerHTML = "";

  (leaderboard || []).forEach((entry) => {
    const item = document.createElement("li");
    item.textContent = `${entry.name} — ${entry.score}`;
    list.appendChild(item);
  });

  showScreen("ended");
}

export function initializeQuiz() {
  elements.musicSubmitBtn.addEventListener("click", submitMusicAnswer);
}

export function restoreRejoinedSession(state) {
  if (!state?.currentSlide) {
    showScreen("waiting");
    return;
  }

  // Rebuild the current question or slide first.
  onSlideStarted(state.currentSlide);

  questionPoints = state.questionPoints ?? 0;

  if (state.isRevealed) {
    stopCountdown();

    // We don't have the returning player's original correctness result
    // in this DTO, so don't incorrectly show a red cross or green tick.
    elements.revealIcon.classList.add("hidden");
    elements.musicRevealResults.classList.add("hidden");

    elements.revealRank.textContent = state.hasAnswered
      ? `+${questionPoints} points`
      : "This round has been revealed";

    showScreen("reveal");
    return;
  }

  if (state.hasAnswered) {
    stopCountdown();
    showScreen("locked");
  }
}

function getRemainingSeconds(slide) {
  if (!slide.deadlineUtc) {
    return slide.timeSeconds;
  }

  return Math.max(
    0,
    (new Date(slide.deadlineUtc).getTime() - Date.now()) / 1000
  );
}

export const quizHandlers = {
  onSlideStarted,
  onSlideRevealed,
  onLeaderboardUpdated,
  onSessionEnded,
  onQuestionPoints,
};