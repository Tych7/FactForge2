using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using FactForge.Data.Entities;
using FactForge.Models;
using FactForge.Services;
using FactForge.Services.Presentation;

namespace FactForge.ViewModels.Presentation;

public partial class PresentViewModel
{
    private QuizSession? _session;

    private async Task InitializeSessionAsync()
    {
        var quiz = await _quizRepository.GetQuizWithSlidesAsync(_quizId);

        if (quiz is null)
        {
            _onBack();
            return;
        }

        QuizTitle = quiz.Title;
        TotalSlides = quiz.Slides.Count;

        var (session, _) = await _presentationService.StartSessionAsync(_quizId);

        _session = session;
        JoinCode = session.JoinCode;

        var ip = TryGetLocalIp();
        JoinUrl = $"http://{ip}:{LocalWebHost.Port}/?code={session.JoinCode}";
        QrImage = _qrCodeService.GenerateQrCode(JoinUrl);
    }

    private string TryGetLocalIp()
    {
        try { return _qrCodeService.GetLocalIpAddress(); }
        catch { return "localhost"; }
    }

    private void OnSessionEnded()
    {
        Dispatcher.UIThread.Post(() =>
        {
            Phase = PresentPhase.Finished;
        });
    }
}