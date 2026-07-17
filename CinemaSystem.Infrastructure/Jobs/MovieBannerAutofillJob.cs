using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CinemaSystem.Infrastructure.Configuration;
using CinemaSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using YoutubeExplode;
using YoutubeExplode.Search;

namespace CinemaSystem.Infrastructure.Jobs;

public class MovieBannerAutofillJob : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MovieBannerAutofillJob> _logger;
    private readonly GeminiSettings _geminiSettings;
    private static readonly HttpClient _httpClient = new HttpClient();

    public class YoutubeVideoInfo
    {
        public string VideoId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
    }

    public MovieBannerAutofillJob(
        IServiceProvider serviceProvider, 
        ILogger<MovieBannerAutofillJob> logger,
        IOptions<GeminiSettings> geminiOptions)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _geminiSettings = geminiOptions?.Value ?? new GeminiSettings();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Chạy lần đầu tiên sau khi khởi động app 10 giây
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();

                _logger.LogInformation("MovieBannerAutofillJob: Quét và cập nhật banner phim tự động bằng AI...");

                // 1. Quét các phim đang chiếu (NOW_SHOWING) chưa có banner
                var nowShowingMoviesWithoutBanner = await dbContext.Movies
                    .Where(m => m.MovieStatus == "NOW_SHOWING" && (m.BannerUrl == null || m.BannerUrl == ""))
                    .ToListAsync(stoppingToken);

                if (nowShowingMoviesWithoutBanner.Any())
                {
                    _logger.LogInformation("MovieBannerAutofillJob: Tìm thấy {Count} phim đang chiếu chưa có banner. Tiến hành tạo banner...", nowShowingMoviesWithoutBanner.Count);
                    
                    var youtube = new YoutubeClient();

                    foreach (var movie in nowShowingMoviesWithoutBanner)
                    {
                        try
                        {
                            string? trailerUrl = movie.TrailerUrl;
                            string? videoId = null;

                            // Nếu phim đã có sẵn TrailerUrl dạng YouTube
                            if (!string.IsNullOrWhiteSpace(trailerUrl))
                            {
                                videoId = ExtractYoutubeVideoId(trailerUrl);
                            }

                            // Nếu chưa có Video ID (chưa có trailer hoặc link không phải Youtube)
                            if (string.IsNullOrEmpty(videoId))
                            {
                                var searchQuery = $"{movie.Title} official trailer";
                                _logger.LogInformation("MovieBannerAutofillJob: Đang tìm trailer YouTube cho phim '{Title}'...", movie.Title);
                                
                                var searchResults = youtube.Search.GetResultsAsync(searchQuery, stoppingToken);
                                var videoList = new List<YoutubeVideoInfo>();
                                
                                await foreach (var result in searchResults)
                                {
                                    if (result is VideoSearchResult videoResult)
                                    {
                                        videoList.Add(new YoutubeVideoInfo
                                        {
                                            VideoId = videoResult.Id.Value,
                                            Title = videoResult.Title,
                                            Author = videoResult.Author.Title
                                        });

                                        if (videoList.Count >= 5) break;
                                    }
                                }

                                if (videoList.Any())
                                {
                                    // Gọi Gemini để phân biệt video nào là trailer chính thức đẹp nhất
                                    videoId = await GetBestTrailerVideoIdWithGeminiAsync(movie.Title, videoList, stoppingToken);

                                    if (string.IsNullOrEmpty(videoId))
                                    {
                                        // Fallback: Nếu Gemini lỗi, chọn video đầu tiên
                                        videoId = videoList[0].VideoId;
                                    }

                                    // Cập nhật lại TrailerUrl cho phim
                                    movie.TrailerUrl = $"https://www.youtube.com/watch?v={videoId}";
                                }
                            }

                            // Thực hiện gán Banner từ YouTube Video ID
                            if (!string.IsNullOrEmpty(videoId))
                            {
                                // Sử dụng maxresdefault.jpg làm Banner
                                movie.BannerUrl = $"https://img.youtube.com/vi/{videoId}/maxresdefault.jpg";
                                _logger.LogInformation("MovieBannerAutofillJob: Đã tạo banner thành công cho phim '{Title}' (VideoID: {VideoID})", movie.Title, videoId);
                            }
                            else
                            {
                                _logger.LogWarning("MovieBannerAutofillJob: Không tìm được YouTube video phù hợp cho phim '{Title}'", movie.Title);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "MovieBannerAutofillJob: Lỗi khi xử lý tạo banner cho phim '{Title}'", movie.Title);
                        }
                    }
                }

                // 2. Gỡ/xóa banner của các phim đã hết chiếu (khác NOW_SHOWING) để không hiển thị trên trang Home
                var endedMoviesWithBanner = await dbContext.Movies
                    .Where(m => m.MovieStatus != "NOW_SHOWING" && m.BannerUrl != null && m.BannerUrl != "")
                    .ToListAsync(stoppingToken);

                if (endedMoviesWithBanner.Any())
                {
                    _logger.LogInformation("MovieBannerAutofillJob: Tìm thấy {Count} phim đã hết chiếu vẫn còn banner. Tiến hành gỡ banner...", endedMoviesWithBanner.Count);
                    foreach (var movie in endedMoviesWithBanner)
                    {
                        movie.BannerUrl = null;
                        _logger.LogInformation("MovieBannerAutofillJob: Đã gỡ banner cho phim '{Title}'", movie.Title);
                    }
                }

                // Lưu các thay đổi vào database
                await dbContext.SaveChangesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MovieBannerAutofillJob: Lỗi xảy ra trong quá trình chạy background job");
            }

            // Chạy định kỳ mỗi 5 phút
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }

    private async Task<string?> GetBestTrailerVideoIdWithGeminiAsync(string movieTitle, List<YoutubeVideoInfo> videos, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_geminiSettings.ApiKey))
        {
            _logger.LogWarning("MovieBannerAutofillJob: Chưa cấu hình Gemini API Key. Bỏ qua phân tích AI.");
            return null;
        }

        try
        {
            var videoListString = string.Join("\n", videos.Select((v, idx) => $"{idx + 1}. ID: {v.VideoId} | Tiêu đề: {v.Title} | Kênh: {v.Author}"));
            
            var promptText = $"Bạn là trợ lý AI chuyên nghiệp của cụm rạp chiếu phim. Nhiệm vụ của bạn là phân tích danh sách các video YouTube tìm kiếm được bên dưới cho bộ phim '{movieTitle}' và lựa chọn ra 1 video duy nhất có khả năng cao nhất là Official Trailer hoặc Official Teaser chính thức từ nhà phát hành hoặc rạp chiếu lớn (như CGV, Galaxy Cinema, Lotte Cinema, các hãng phim lớn như Marvel, Disney, Sony, Warner Bros, Hoan Khue, v.v.).\n" +
                             "Lưu ý quan trọng: Hãy TRÁNH các video review phim, reaction, fan-made, video ghép ảnh tĩnh, phân tích cốt truyện, hoặc video của các kênh YouTuber cá nhân vì ảnh thumbnail của chúng thường bị chèn chữ rác, ghép mặt người review rất xấu và không phù hợp làm banner trang chủ.\n\n" +
                             "Danh sách video:\n" + videoListString + "\n\n" +
                             "Hãy chọn video tốt nhất và trả về kết quả định dạng JSON.";

            var payload = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = promptText }
                        }
                    }
                },
                generationConfig = new
                {
                    responseMimeType = "application/json",
                    responseSchema = new
                    {
                        type = "OBJECT",
                        properties = new
                        {
                            selectedVideoId = new { type = "STRING", description = "ID của video được chọn làm trailer chính thức." },
                            reason = new { type = "STRING", description = "Lý do lựa chọn video này." }
                        },
                        required = new[] { "selectedVideoId" }
                    }
                }
            };

            var url = $"{_geminiSettings.ApiBaseUrl.TrimEnd('/')}/{Uri.EscapeDataString(_geminiSettings.Model)}:generateContent";
            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = JsonContent.Create(payload)
            };
            request.Headers.TryAddWithoutValidation(GeminiSettings.ApiKeyHeaderName, _geminiSettings.ApiKey);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var errContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("MovieBannerAutofillJob: Lỗi gọi Gemini API: {Status} - {Err}", response.StatusCode, errContent);
                return null;
            }

            var jsonDoc = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
            var replyText = jsonDoc.GetProperty("candidates")[0]
                                   .GetProperty("content")
                                   .GetProperty("parts")[0]
                                   .GetProperty("text").GetString();

            if (string.IsNullOrWhiteSpace(replyText)) return null;

            using var doc = JsonDocument.Parse(replyText);
            if (doc.RootElement.TryGetProperty("selectedVideoId", out var idProp))
            {
                var id = idProp.GetString();
                if (videos.Any(v => v.VideoId == id))
                {
                    if (doc.RootElement.TryGetProperty("reason", out var reasonProp))
                    {
                        _logger.LogInformation("MovieBannerAutofillJob: Gemini đã chọn video {VideoID}. Lý do: {Reason}", id, reasonProp.GetString());
                    }
                    return id;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MovieBannerAutofillJob: Lỗi trong quá trình AI phân tích chọn trailer tốt nhất");
        }

        return null;
    }

    private static string? ExtractYoutubeVideoId(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;

        var match = System.Text.RegularExpressions.Regex.Match(
            url,
            @"(?:youtu\.be\/|youtube\.com\/(?:embed\/|v\/|watch\?v=|watch\?.+&v=))([\w-]{11})",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        return match.Success ? match.Groups[1].Value : null;
    }
}
