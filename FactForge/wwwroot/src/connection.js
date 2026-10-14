export function createConnection(handlers) {
  const connection = new signalR.HubConnectionBuilder()
    .withUrl("/quizhub")
    .withAutomaticReconnect()
    .build();

  connection.on("SlideStarted", handlers.onSlideStarted);
  connection.on("SlideRevealed", handlers.onSlideRevealed);
  connection.on("LeaderboardUpdated", handlers.onLeaderboardUpdated);
  connection.on("SessionEnded", handlers.onSessionEnded);

  connection.on("QuestionPoints", handlers.onQuestionPoints);

  return connection;
}