let countdownHandle = null;

export function startTimerCountdown(totalSeconds, progressBar) {
  startCountdown(totalSeconds, progressBar);
}

export function startMusicCountdown(totalSeconds, progressBar) {
  startCountdown(totalSeconds, progressBar);
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

    const percentage = totalMs > 0
      ? Math.min(100, (remainingMs / totalMs) * 100)
      : 0;

    progressBar.style.width = `${percentage}%`;

    if (remainingMs <= 0) {
      stopCountdown();
    }
  };

  update();
  countdownHandle = setInterval(update, 200);
}

export function stopCountdown() {
  if (countdownHandle !== null) {
    clearInterval(countdownHandle);
    countdownHandle = null;
  }
}