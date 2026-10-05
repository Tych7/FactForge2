using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FactForge.Data;
using FactForge.Data.Entities;
using FactForge.Models;
using Microsoft.EntityFrameworkCore;

namespace FactForge.Services;

// CRUD for quizzes/slides, backed by SQLite via EF Core. Each method opens its own
// short-lived DbContext (via the factory) so it's safe to call from the UI thread
// and from background/hub code without sharing a context across threads.
public class QuizRepository
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public QuizRepository(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<List<Quiz>> GetAllQuizzesAsync()
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        return await db.Quizzes.OrderByDescending(q => q.UpdatedAt).ToListAsync();
    }

    public async Task<Quiz?> GetQuizWithSlidesAsync(int quizId)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        return await db.Quizzes
            .Include(q => q.Slides.OrderBy(s => s.OrderIndex))
            .ThenInclude(s => s.Options.OrderBy(o => o.OrderIndex))
            .FirstOrDefaultAsync(q => q.Id == quizId);
    }

    public async Task<Quiz> CreateQuizAsync(string title)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var now = DateTime.UtcNow;
        var quiz = new Quiz { Title = title, CreatedAt = now, UpdatedAt = now };
        db.Quizzes.Add(quiz);
        await db.SaveChangesAsync();
        return quiz;
    }

    public async Task RenameQuizAsync(int quizId, string title)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var quiz = await db.Quizzes.FindAsync(quizId);
        if (quiz is null) return;
        quiz.Title = title;
        quiz.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    public async Task DeleteQuizAsync(int quizId)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var quiz = await db.Quizzes.FindAsync(quizId);
        if (quiz is null) return;
        db.Quizzes.Remove(quiz);
        await db.SaveChangesAsync();
    }

    public async Task<Slide> AddSlideAsync(int quizId, SlideType type, int? insertAtIndex = null)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var slides = await db.Slides.Where(s => s.QuizId == quizId).OrderBy(s => s.OrderIndex).ToListAsync();

        var index = insertAtIndex.HasValue && insertAtIndex.Value >= 0 && insertAtIndex.Value <= slides.Count
            ? insertAtIndex.Value
            : slides.Count;

        var slide = new Slide
        {
            QuizId = quizId,
            OrderIndex = index,
            Type = type,
            Header = type == SlideType.Text ? string.Empty : null,
            SubText = type == SlideType.Text ? string.Empty : null,
            Question = type is SlideType.MultipleChoice or SlideType.OpenQuestion ? string.Empty : null,
            CorrectAnswer = type is SlideType.MultipleChoice or SlideType.OpenQuestion ? string.Empty : null,
            TimeSeconds = 20
        };

        if (type == SlideType.MultipleChoice)
        {
            slide.Options = Enumerable.Range(0, 4)
                .Select(i => new SlideOption { OrderIndex = i, Text = string.Empty })
                .ToList();
        }

        db.Slides.Add(slide);

        // shift everything at/after the insertion point down by one
        foreach (var s in slides.Where(s => s.OrderIndex >= index))
            s.OrderIndex++;
        db.Slides.UpdateRange(slides);

        await TouchQuizAsync(db, quizId);
        await db.SaveChangesAsync();
        return slide;
    }

    public async Task DeleteSlideAsync(int slideId)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var slide = await db.Slides.FindAsync(slideId);
        if (slide is null) return;

        var quizId = slide.QuizId;
        db.Slides.Remove(slide);
        await db.SaveChangesAsync();

        var remaining = await db.Slides.Where(s => s.QuizId == quizId).OrderBy(s => s.OrderIndex).ToListAsync();
        for (var i = 0; i < remaining.Count; i++)
            remaining[i].OrderIndex = i;

        await TouchQuizAsync(db, quizId);
        await db.SaveChangesAsync();
    }

    public async Task ReorderSlideAsync(int quizId, int slideId, int newIndex)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var slides = await db.Slides.Where(s => s.QuizId == quizId).OrderBy(s => s.OrderIndex).ToListAsync();
        var slide = slides.FirstOrDefault(s => s.Id == slideId);
        if (slide is null) return;

        slides.Remove(slide);
        newIndex = Math.Clamp(newIndex, 0, slides.Count);
        slides.Insert(newIndex, slide);

        for (var i = 0; i < slides.Count; i++)
            slides[i].OrderIndex = i;

        await TouchQuizAsync(db, quizId);
        await db.SaveChangesAsync();
    }

    public async Task SaveSlideAsync(Slide slide)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var existing = await db.Slides.Include(s => s.Options).FirstOrDefaultAsync(s => s.Id == slide.Id);
        if (existing is null) return;

        existing.Header = slide.Header;
        existing.SubText = slide.SubText;
        existing.Question = slide.Question;
        existing.CorrectAnswer = slide.CorrectAnswer;
        existing.TimeSeconds = slide.TimeSeconds;
        existing.ImagePath = slide.ImagePath;
        existing.MusicFilePath = slide.MusicFilePath;
        existing.Title = slide.Title;
        existing.Artist = slide.Artist;

        existing.ImagePath = slide.ImagePath;

        db.SlideOptions.RemoveRange(existing.Options);
        existing.Options = slide.Options
            .Select((o, i) => new SlideOption { SlideId = slide.Id, OrderIndex = i, Text = o.Text, IsCorrect = o.IsCorrect })
            .ToList();

        await TouchQuizAsync(db, existing.QuizId);
        await db.SaveChangesAsync();
    }

    public async Task<List<QuizSession>> GetSessionsForQuizAsync(int quizId)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        return await db.QuizSessions
            .Where(s => s.QuizId == quizId)
            .Include(s => s.Players).ThenInclude(p => p.Answers)
            .OrderByDescending(s => s.StartedAt)
            .ToListAsync();
    }

    private static async Task TouchQuizAsync(AppDbContext db, int quizId)
    {
        var quiz = await db.Quizzes.FindAsync(quizId);
        if (quiz is not null) quiz.UpdatedAt = DateTime.UtcNow;
    }
}
