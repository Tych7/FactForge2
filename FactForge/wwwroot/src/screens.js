const SlideType = {
  Text: 0,
  MultipleChoice: 1,
  OpenQuestion: 2,
  MusicQuestion: 3,
  Leaderboard: 4,
};

export const screens = {
  join: document.getElementById("screen-join"),
  waiting: document.getElementById("screen-waiting"),
  textSlide: document.getElementById("screen-textslide"),
  question: document.getElementById("screen-question"),
  musicQuestion: document.getElementById("screen-music-question"),
  locked: document.getElementById("screen-locked"),
  reveal: document.getElementById("screen-reveal"),
  ended: document.getElementById("screen-ended"),
};

export function showScreen(name) {
  for (const [key, element] of Object.entries(screens)) {
    element.classList.toggle("hidden", key !== name);
  }
}

export const elements = {
  joinCodeInput: document.getElementById("joinCode"),
  playerNameInput: document.getElementById("playerName"),
  joinBtn: document.getElementById("joinBtn"),
  joinError: document.getElementById("joinError"),
  rejoinOptions: document.getElementById("rejoinOptions"),
  rejoinPlayers: document.getElementById("rejoinPlayers"),
  joinAsNewBtn: document.getElementById("joinAsNewBtn"),

  questionText: document.getElementById("questionText"),
  choices: document.getElementById("choices"),
  timerBar: document.getElementById("timerBar"),

  revealIcon: document.getElementById("revealIcon"),
  revealRank: document.getElementById("revealRank"),
  finalLeaderboard: document.getElementById("finalLeaderboard"),

  musicArtistInput: document.getElementById("musicArtist"),
  musicTitleInput: document.getElementById("musicTitle"),
  musicSubmitBtn: document.getElementById("musicSubmitBtn"),
  musicTimerBar: document.getElementById("musicTimerBar"),

  musicRevealResults: document.getElementById("musicRevealResults"),
  musicArtistResult: document.getElementById("musicArtistResult"),
  musicTitleResult: document.getElementById("musicTitleResult"),
};

export { SlideType };