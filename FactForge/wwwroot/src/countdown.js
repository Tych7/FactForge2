let animationFrame = null;
let countdownStartedAt = 0;
let countdownDuration = 0;
let activeProgressBar = null;

export function startTimerCountdown(totalSeconds, progressBar) {
  startCountdown(totalSeconds, progressBar);
}

export function startMusicCountdown(totalSeconds, progressBar) {
  startCountdown(totalSeconds, progressBar);
}

function startCountdown(totalSeconds, progressBar) {
  stopCountdown();

  countdownDuration = Math.max(0, totalSeconds * 1000);
  countdownStartedAt = performance.now();
  activeProgressBar = progressBar;

  const update = (now) => {
    if (!activeProgressBar) return;

    const elapsed = now - countdownStartedAt;
    const remaining = Math.max(0, countdownDuration - elapsed);

    const percentage = countdownDuration > 0
      ? (remaining / countdownDuration) * 100
      : 0;

    activeProgressBar.style.width = `${percentage}%`;

    if (remaining > 0) {
      animationFrame = requestAnimationFrame(update);
    } else {
      animationFrame = null;
      activeProgressBar = null;
    }
  };

  animationFrame = requestAnimationFrame(update);
}

export function stopCountdown() {
  if (animationFrame !== null) {
    cancelAnimationFrame(animationFrame);
    animationFrame = null;
  }

  activeProgressBar = null;
}