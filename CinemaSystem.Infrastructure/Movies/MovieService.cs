using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Application.Settings;
using CinemaSystem.Contracts.Common;
using CinemaSystem.Contracts.Movies;
using CinemaSystem.Domain.Constants;
using CinemaSystem.Domain.Entities;
using CinemaSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using YoutubeExplode;

namespace CinemaSystem.Infrastructure.Movies;

public sealed class MovieService : IMovieService
{
    private readonly CinemaDbContext _dbContext;
    private readonly IAdminRefundService _refundService;
    private readonly IFileStorageService _fileStorageService;
    private readonly CinemaSystem.Application.Settings.CinemaProcessingSettings _settings;
    private readonly FileStorageSettings _fileStorageSettings;
    private readonly Configuration.GeminiSettings _geminiSettings;

    public MovieService(
        CinemaDbContext dbContext,
        IAdminRefundService refundService,
        IFileStorageService fileStorageService,
        Microsoft.Extensions.Options.IOptions<CinemaSystem.Application.Settings.CinemaProcessingSettings> options,
        Microsoft.Extensions.Options.IOptions<FileStorageSettings> fileStorageOptions)
        : this(dbContext, refundService, fileStorageService, options, fileStorageOptions, Microsoft.Extensions.Options.Options.Create(new Configuration.GeminiSettings()))
    {
    }

    public MovieService(
        CinemaDbContext dbContext,
        IAdminRefundService refundService,
        IFileStorageService fileStorageService,
        Microsoft.Extensions.Options.IOptions<CinemaSystem.Application.Settings.CinemaProcessingSettings> options,
        Microsoft.Extensions.Options.IOptions<FileStorageSettings> fileStorageOptions,
        Microsoft.Extensions.Options.IOptions<Configuration.GeminiSettings> geminiOptions)
    {
        _dbContext = dbContext;
        _refundService = refundService;
        _fileStorageService = fileStorageService;
        _settings = options.Value;
        _fileStorageSettings = fileStorageOptions.Value;
        _geminiSettings = geminiOptions.Value;
    }

    public async Task<ServiceResult<PagedList<MovieResponse>>> GetMoviesAsync(
        string? status,
        int pageIndex,
        int pageSize,
        string? genre,
        bool includeDeleted,
        CancellationToken cancellationToken)
    {
        // Khởi tạo truy vấn bảng Movies, bao gồm bảng trung gian MovieGenres và bảng Genres, không theo dõi sự thay đổi (AsNoTracking) để tối ưu hiệu suất đọc
        var query = _dbContext.Movies.Include(m => m.MovieGenres).ThenInclude(mg => mg.Genre).AsNoTracking();

        // Nếu tham số thể loại (genre) được truyền vào hợp lệ
        if (!string.IsNullOrWhiteSpace(genre))
        {
            // Lọc các bộ phim có chứa thể loại khớp với tham số truyền vào
            query = query.Where(m => m.MovieGenres.Any(mg => mg.Genre.Name.Contains(genre)));
        }

        // Nếu không bao gồm các bộ phim đã xóa (hoặc bị ẩn)
        if (!includeDeleted)
        {
            // Lọc bỏ các bộ phim có trạng thái Inactive và các bộ phim có nhãn độ tuổi C
            query = query.Where(movie => movie.MovieStatus != DomainConstants.EntityStatus.Inactive && movie.AgeRating != DomainConstants.AgeRating.C);
        }

        // Nếu tham số trạng thái được truyền vào hợp lệ
        if (!string.IsNullOrWhiteSpace(status))
        {
            // Chuẩn hóa trạng thái về dạng chữ in hoa không khoảng trắng thừa
            var normalizedStatus = status.Trim().ToUpperInvariant();
            // Lọc các bộ phim theo trạng thái chuẩn hóa
            query = query.Where(movie => movie.MovieStatus == normalizedStatus);
        }

        // Thực thi đếm tổng số lượng bản ghi thỏa mãn điều kiện (Dùng cho phân trang)
        var totalCount = await query.CountAsync(cancellationToken);

        // Thực thi truy vấn lấy dữ liệu
        var movies = await query
            // Sắp xếp các bộ phim mới nhất lên đầu (Giảm dần theo ngày phát hành)
            .OrderByDescending(movie => movie.ReleaseDate)
            // Phân trang: Bỏ qua các bản ghi ở các trang trước
            .Skip((pageIndex - 1) * pageSize)
            // Phân trang: Chỉ lấy đủ số lượng bản ghi của 1 trang
            .Take(pageSize)
            // Ánh xạ dữ liệu từ Entity (SQL) sang DTO (Data Transfer Object) để trả về API
            .Select(movie => new MovieResponse
            {
                // Gán ID bộ phim
                Id = movie.MovieId,
                // Gán tên bộ phim (Tiếng Việt)
                MovieNameVn = movie.Title,
                // Lấy danh sách tên các thể loại của bộ phim
                Genres = movie.MovieGenres.Select(mg => mg.Genre.Name).ToList(),
                // Gán thời lượng phim (phút)
                Duration = movie.DurationMinutes,
                // Gán đường dẫn ảnh poster phim
                ImagePoster = movie.PosterUrl,
                // Gán điểm đánh giá trung bình
                AvgRating = movie.AverageRating,
                // Gán độ nổi bật của phim (Hot, Trending, Popular...)
                Highlight = movie.Highlight,
                // Gán số lượt xem
                ViewCount = movie.ViewCount,
                // Gán nhãn độ tuổi quy định
                AgeRating = movie.AgeRating,
                // Gán trạng thái hiện tại của bộ phim
                MovieStatus = movie.MovieStatus,
                // Gán tên đạo diễn
                Director = movie.Director
            })
            // Chạy bất đồng bộ và chuyển đổi thành danh sách (List)
            .ToListAsync(cancellationToken);

        // Khởi tạo đối tượng Phân trang bao bọc danh sách DTO
        var pagedList = new PagedList<MovieResponse>(movies, totalCount, pageIndex, pageSize);

        // Trả về kết quả cho Controller
        return ServiceResult<PagedList<MovieResponse>>.Ok(
            pagedList,
            "Movies retrieved successfully.");
    }

    public async Task<ServiceResult<MovieDetailResponse>> GetMovieByIdAsync(
        string movieId,
        bool isAdmin,
        CancellationToken cancellationToken)
    {
        // Khởi tạo truy vấn bảng Movies, bao gồm bảng trung gian MovieGenres và bảng Genres, lọc theo ID bộ phim
        var query = _dbContext.Movies.Include(m => m.MovieGenres).ThenInclude(mg => mg.Genre).Where(item => item.MovieId == movieId);
        
        // Nếu không phải là quyền Admin
        if (!isAdmin)
        {
            // Ẩn đi những phim có trạng thái Inactive hoặc có nhãn độ tuổi C
            query = query.Where(item => item.MovieStatus != DomainConstants.EntityStatus.Inactive && item.AgeRating != DomainConstants.AgeRating.C);
        }

        // Thực thi truy vấn lấy bản ghi đầu tiên khớp điều kiện, hoặc null nếu không tìm thấy
        var movie = await query.FirstOrDefaultAsync(cancellationToken);

        // Nếu không tìm thấy bộ phim
        if (movie is null)
        {
            // Trả về kết quả thất bại kèm thông báo lỗi
            return ServiceResult<MovieDetailResponse>.Fail(
                404,
                "Movie was not found.",
                "MOVIE_NOT_FOUND");
        }

        // Ánh xạ dữ liệu từ Entity sang DTO chi tiết
        var response = ToDetailResponse(movie);

        // Trả về kết quả thành công cho Controller
        return ServiceResult<MovieDetailResponse>.Ok(
            response,
            "Movie retrieved successfully.");
    }

    public async Task<ServiceResult<object>> IncrementMovieViewAsync(
        string movieId,
        CancellationToken cancellationToken)
    {
        // Tìm kiếm bộ phim theo ID
        var movie = await _dbContext.Movies.FirstOrDefaultAsync(item => item.MovieId == movieId, cancellationToken);
        // Kiểm tra nếu không tìm thấy bộ phim
        if (movie is null)
        {
            // Trả về kết quả thất bại
            return ServiceResult<object>.Fail(404, "Movie was not found.", "MOVIE_NOT_FOUND");
        }

        // Tăng số lượt xem của bộ phim lên 1
        movie.ViewCount += 1;

        // Lấy số lượt xem cao nhất hiện tại trong toàn bộ danh sách phim (trả về 0 nếu chưa có dữ liệu)
        var maxViews = await _dbContext.Movies.MaxAsync(m => (int?)m.ViewCount, cancellationToken) ?? 0;

        // Nếu bộ phim hiện tại đạt lượt xem bằng mức cao nhất và lớn hơn 0
        if (movie.ViewCount >= maxViews && movie.ViewCount > 0)
        {
            // Tìm các bộ phim khác đang được đánh dấu là Popular (phổ biến) để hạ cấp
            var previousPopular = await _dbContext.Movies
                .Where(m => m.Highlight == DomainConstants.MovieHighlight.Popular && m.MovieId != movie.MovieId)
                .ToListAsync(cancellationToken);

            // Duyệt qua danh sách phim Popular cũ
            foreach (var p in previousPopular)
            {
                // Hạ cấp đánh dấu dựa trên ngưỡng lượt xem quy định (Hot, Trending hoặc xóa đánh dấu)
                p.Highlight = p.ViewCount >= _settings.MovieHotViewThreshold ? DomainConstants.MovieHighlight.Hot : (p.ViewCount >= _settings.MovieTrendingViewThreshold ? DomainConstants.MovieHighlight.Trending : null);
            }

            // Đánh dấu bộ phim hiện tại là Popular
            movie.Highlight = DomainConstants.MovieHighlight.Popular;
        }
        // Nếu phim hiện tại chưa đạt mức Popular
        else if (movie.Highlight != DomainConstants.MovieHighlight.Popular)
        {
            // Nếu lượt xem vượt ngưỡng Hot
            if (movie.ViewCount >= _settings.MovieHotViewThreshold)
            {
                // Đánh dấu phim là Hot
                movie.Highlight = DomainConstants.MovieHighlight.Hot;
            }
            // Nếu lượt xem vượt ngưỡng Trending
            else if (movie.ViewCount >= _settings.MovieTrendingViewThreshold)
            {
                // Đánh dấu phim là Trending
                movie.Highlight = DomainConstants.MovieHighlight.Trending;
            }
        }

        // Lưu toàn bộ thay đổi (số lượt xem và trạng thái nổi bật) vào cơ sở dữ liệu
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Trả về kết quả thành công kèm thông tin ID và số lượt xem mới
        return ServiceResult<object>.Ok(new { MovieId = movieId, ViewCount = movie.ViewCount }, "Movie view incremented.");
    }

    private static MovieDetailResponse ToDetailResponse(Movie movie)
    {
        // Ánh xạ đối tượng Entity Movie sang đối tượng MovieDetailResponse (DTO)
        return new MovieDetailResponse
        {
            // Gán ID bộ phim
            MovieId = movie.MovieId,
            // Gán tiêu đề bộ phim
            Title = movie.Title,
            // Gán thời lượng (phút)
            DurationMinutes = movie.DurationMinutes,
            // Lấy danh sách tên thể loại
            Genres = movie.MovieGenres.Select(mg => mg.Genre.Name).ToList(),
            // Gán mã ngôn ngữ
            Language = movie.LanguageId,
            // Gán ngày phát hành
            ReleaseDate = movie.ReleaseDate,
            // Gán điểm đánh giá trung bình
            AvgRating = movie.AverageRating,
            // Gán mô tả chi tiết phim
            Description = movie.Description,
            // Gán đường dẫn ảnh poster
            PosterUrl = movie.PosterUrl,
            // Gán đường dẫn video trailer
            TrailerUrl = movie.TrailerUrl,
            // Gán trạng thái bộ phim
            MovieStatus = movie.MovieStatus,
            // Gán số lượt xem
            ViewCount = movie.ViewCount,
            // Gán nhãn độ tuổi
            AgeRating = movie.AgeRating,
            // Gán tên đạo diễn
            Director = movie.Director
        };
    }

    public async Task<ServiceResult<MovieDetailResponse>> CreateMovieAsync(
        CreateMovieRequest request,
        Stream? posterStream,
        string? posterFileName,
        CancellationToken cancellationToken)
    {
        // 1. Kiểm tra tiêu đề bị trùng lặp
        var exists = await _dbContext.Movies.AnyAsync(m => m.Title == request.Title, cancellationToken);
        if (exists)
        {
            // Trả về lỗi nếu đã tồn tại phim cùng tên
            return ServiceResult<MovieDetailResponse>.Fail(400, "A movie with this title already exists.", "MOVIE_TITLE_DUPLICATED");
        }

        // 2. Xác thực Nhãn độ tuổi (AgeRating)
        if (!string.IsNullOrEmpty(request.AgeRating) && !DomainConstants.AgeRating.ValidRatings.Contains(request.AgeRating.ToUpperInvariant()))
        {
            // Trả về lỗi nếu nhãn độ tuổi không hợp lệ
            return ServiceResult<MovieDetailResponse>.Fail(400, "Invalid Age Rating. Allowed values: P, K, T13, T16, T18, C.", "INVALID_AGE_RATING");
        }

        // 2.1 Xác thực Ngôn ngữ (Language)
        if (!string.IsNullOrEmpty(request.Language))
        {
            // Kiểm tra xem ngôn ngữ có tồn tại trong cơ sở dữ liệu không
            var validLanguage = await _dbContext.Languages.AnyAsync(l => l.LanguageId == request.Language.ToUpperInvariant(), cancellationToken);
            if (!validLanguage)
            {
                // Trả về lỗi nếu mã ngôn ngữ không hợp lệ
                return ServiceResult<MovieDetailResponse>.Fail(400, "Invalid Language.", "INVALID_LANGUAGE");
            }
        }

        // 2.2 Xác thực Thể loại (Genres)
        // Khởi tạo tập hợp lưu trữ ID các thể loại hợp lệ
        var finalGenreIds = new HashSet<int>();
        
        // Nếu request có truyền danh sách ID thể loại
        if (request.GenreIds != null && request.GenreIds.Any())
        {
            // Đếm số lượng ID thể loại tồn tại trong DB
            var validGenreCount = await _dbContext.Genres.CountAsync(g => request.GenreIds.Contains(g.GenreId), cancellationToken);
            // Nếu số lượng tìm thấy không khớp với số lượng truyền vào (đã loại bỏ trùng lặp)
            if (validGenreCount != request.GenreIds.Distinct().Count())
            {
                // Trả về lỗi do có ID thể loại không hợp lệ
                return ServiceResult<MovieDetailResponse>.Fail(400, "One or more provided GenreIds are invalid.", "INVALID_GENRE_ID");
            }
            // Thêm các ID thể loại hợp lệ vào tập hợp
            foreach (var id in request.GenreIds) finalGenreIds.Add(id);
        }

        // Nếu request có truyền danh sách Tên thể loại
        if (request.GenreNames != null && request.GenreNames.Any())
        {
            // Duyệt qua từng tên thể loại không trùng lặp
            foreach (var genreName in request.GenreNames.Distinct())
            {
                // Bỏ qua nếu tên rỗng
                if (string.IsNullOrWhiteSpace(genreName)) continue;
                // Chuẩn hóa loại bỏ khoảng trắng thừa
                var trimmedName = genreName.Trim();
                // Tìm kiếm thể loại trong DB theo tên (không phân biệt hoa thường)
                var existingGenre = await _dbContext.Genres.FirstOrDefaultAsync(g => g.Name.ToLower() == trimmedName.ToLower(), cancellationToken);
                
                // Nếu thể loại đã tồn tại
                if (existingGenre != null)
                {
                    // Thêm ID thể loại vào tập hợp
                    finalGenreIds.Add(existingGenre.GenreId);
                }
                else
                {
                    // Nếu thể loại chưa tồn tại, tạo mới
                    var newGenre = new Genre { Name = trimmedName };
                    _dbContext.Genres.Add(newGenre);
                    // Lưu ngay vào DB để lấy được ID phát sinh
                    await _dbContext.SaveChangesAsync(cancellationToken);
                    // Thêm ID thể loại mới vào tập hợp
                    finalGenreIds.Add(newGenre.GenreId);
                }
            }
        }

        // 3. Phân tích và xác thực Ngày phát hành
        DateOnly? releaseDate = null;
        if (!string.IsNullOrWhiteSpace(request.ReleaseDate))
        {
            // Thử chuyển đổi chuỗi ngày phát hành sang kiểu DateOnly
            if (DateOnly.TryParse(request.ReleaseDate, out var pd))
            {
                releaseDate = pd;
            }
            else
            {
                // Trả về lỗi nếu định dạng ngày không hợp lệ
                return ServiceResult<MovieDetailResponse>.Fail(400, "Invalid Release Date format. Please use yyyy-MM-dd.", "INVALID_DATE_FORMAT");
            }
        }

        // 4. Tính toán Trạng thái bộ phim (Movie Status)
        // Mặc định trạng thái là "Đang chiếu" (NowShowing)
        string status = DomainConstants.EntityStatus.NowShowing;
        if (!string.IsNullOrWhiteSpace(request.MovieStatus))
        {
            // Nếu Quản trị viên (Admin) truyền vào trạng thái cụ thể thì sử dụng trạng thái đó
            status = request.MovieStatus;
        }
        else if (releaseDate.HasValue && releaseDate.Value > DateOnly.FromDateTime(DateTime.UtcNow))
        {
            // Nếu không truyền trạng thái và ngày phát hành lớn hơn ngày hiện tại, chuyển thành "Sắp chiếu" (ComingSoon)
            status = DomainConstants.EntityStatus.ComingSoon;
        }

        // Tạo ID mới cho bộ phim với tiền tố MOV_
        var movieId = $"{DomainConstants.EntityIdPrefix.Movie}_{Guid.NewGuid():N}";

        // Khởi tạo biến lưu đường dẫn poster
        string? posterUrl = null;
        // Nếu có file ảnh được upload
        if (posterStream != null && !string.IsNullOrWhiteSpace(posterFileName))
        {
            // Lưu file ảnh poster qua dịch vụ lưu trữ (S3/Local) và nhận về URL
            posterUrl = await _fileStorageService.SaveFileAsync(
                posterStream,
                posterFileName,
                _fileStorageSettings.PosterFolder,
                cancellationToken);
        }
        else if (!string.IsNullOrWhiteSpace(request.PosterUrl))
        {
            // Sử dụng URL poster bên ngoài (ví dụ được Gemini trích xuất)
            posterUrl = request.PosterUrl;
        }

        // Tạo mới đối tượng Movie Entity với các thông tin đã chuẩn bị
        var movie = new Movie
        {
            MovieId = movieId,
            Title = request.Title,
            DurationMinutes = request.DurationMinutes,
            LanguageId = request.Language?.ToUpperInvariant(),
            ReleaseDate = releaseDate,
            AgeRating = request.AgeRating?.ToUpperInvariant(),
            Description = request.Description,
            TrailerUrl = request.TrailerUrl,
            Highlight = request.Highlight,
            PosterUrl = posterUrl,
            Director = request.Director,
            MovieStatus = status
        };

        // Gán các thể loại phim vào bảng liên kết trung gian
        if (finalGenreIds.Any())
        {
            foreach (var genreId in finalGenreIds)
            {
                movie.MovieGenres.Add(new MovieGenre { MovieId = movieId, GenreId = genreId });
            }
        }

        // Đưa đối tượng Movie vào Entity Framework để theo dõi
        _dbContext.Movies.Add(movie);

        try
        {
            // Lưu toàn bộ thay đổi xuống Database
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception)
        {
            // Khôi phục (Rollback) file poster đã lưu nếu tiến trình cập nhật Database gặp lỗi
            if (!string.IsNullOrEmpty(posterUrl))
            {
                await _fileStorageService.DeleteFileAsync(posterUrl, CancellationToken.None);
            }
            throw; // Ném lỗi lên trên để Middleware quản lý lỗi xử lý
        }

        var createdMovie = await _dbContext.Movies
            .AsNoTracking()
            .Include(item => item.MovieGenres)
                .ThenInclude(item => item.Genre)
            .SingleAsync(item => item.MovieId == movieId, cancellationToken);

        // Trả về kết quả thành công cùng thông tin chi tiết phim vừa tạo
        return ServiceResult<MovieDetailResponse>.Ok(
            ToDetailResponse(createdMovie),
            "Movie created successfully.");
    }

    public async Task<ServiceResult<MovieDetailResponse>> UpdateMovieAsync(
        string movieId,
        UpdateMovieRequest request,
        Stream? posterStream,
        string? posterFileName,
        string actionUserId,
        CancellationToken cancellationToken)
    {
        // Truy vấn bộ phim theo ID, bao gồm danh sách thể loại liên kết
        var movie = await _dbContext.Movies.Include(m => m.MovieGenres).ThenInclude(mg => mg.Genre).FirstOrDefaultAsync(m => m.MovieId == movieId, cancellationToken);
        // Kiểm tra nếu không tìm thấy bộ phim
        if (movie == null)
        {
            return ServiceResult<MovieDetailResponse>.Fail(404, "Movie was not found.", "MOVIE_NOT_FOUND");
        }

        // Xác thực ngôn ngữ nếu có cập nhật
        if (!string.IsNullOrEmpty(request.Language))
        {
            var validLanguage = await _dbContext.Languages.AnyAsync(l => l.LanguageId == request.Language.ToUpperInvariant(), cancellationToken);
            if (!validLanguage)
            {
                // Trả về lỗi nếu mã ngôn ngữ không tồn tại
                return ServiceResult<MovieDetailResponse>.Fail(400, "Invalid Language.", "INVALID_LANGUAGE");
            }
        }

        // Xử lý cập nhật danh sách thể loại phim (Genres)
        var finalGenreIds = new HashSet<int>();
        // Nếu có truyền danh sách ID thể loại
        if (request.GenreIds != null && request.GenreIds.Any())
        {
            // Đếm số ID thể loại hợp lệ trong CSDL
            var validGenreCount = await _dbContext.Genres.CountAsync(g => request.GenreIds.Contains(g.GenreId), cancellationToken);
            if (validGenreCount != request.GenreIds.Distinct().Count())
            {
                // Trả về lỗi nếu có ID không hợp lệ
                return ServiceResult<MovieDetailResponse>.Fail(400, "One or more provided GenreIds are invalid.", "INVALID_GENRE_ID");
            }
            // Thêm các ID hợp lệ vào tập hợp
            foreach (var id in request.GenreIds) finalGenreIds.Add(id);
        }

        // Nếu có truyền danh sách tên thể loại
        if (request.GenreNames != null && request.GenreNames.Any())
        {
            foreach (var genreName in request.GenreNames.Distinct())
            {
                if (string.IsNullOrWhiteSpace(genreName)) continue;
                var trimmedName = genreName.Trim();
                // Kiểm tra tên thể loại đã tồn tại chưa
                var existingGenre = await _dbContext.Genres.FirstOrDefaultAsync(g => g.Name.ToLower() == trimmedName.ToLower(), cancellationToken);
                if (existingGenre != null)
                {
                    // Nếu tồn tại thì lấy ID
                    finalGenreIds.Add(existingGenre.GenreId);
                }
                else
                {
                    // Nếu chưa tồn tại, tạo mới lưu vào DB và lấy ID
                    var newGenre = new Genre { Name = trimmedName };
                    _dbContext.Genres.Add(newGenre);
                    await _dbContext.SaveChangesAsync(cancellationToken);
                    finalGenreIds.Add(newGenre.GenreId);
                }
            }
        }

        // Kiểm tra xem thời lượng phim (Duration) có bị thay đổi không
        if (movie.DurationMinutes != request.DurationMinutes)
        {
            // Chỉ chặn thay đổi thời lượng phim khi phim đã có suất chiếu có khách đặt vé (booking hoạt động, không bị hủy)
            var hasBookings = await _dbContext.Bookings
                .AnyAsync(b => b.Showtime != null 
                    && b.Showtime.MovieId == movieId 
                    && b.BookingStatus != DomainConstants.BookingStatus.Cancelled, cancellationToken);

            if (hasBookings)
            {
                return ServiceResult<MovieDetailResponse>.Fail(400, "Không thể thay đổi thời lượng phim vì phim đã có suất chiếu có khách đặt vé.", "DURATION_CANNOT_BE_CHANGED_HAS_SHOWTIMES");
            }
        }

        // Xử lý cập nhật Poster phim
        if (posterStream != null && !string.IsNullOrWhiteSpace(posterFileName))
        {
            // Nếu bộ phim đã có ảnh poster trước đó
            if (!string.IsNullOrEmpty(movie.PosterUrl))
            {
                // Xóa ảnh poster cũ khỏi dịch vụ lưu trữ
                await _fileStorageService.DeleteFileAsync(movie.PosterUrl, cancellationToken);
            }
            // Lưu ảnh poster mới và lấy URL
            movie.PosterUrl = await _fileStorageService.SaveFileAsync(
                posterStream,
                posterFileName,
                _fileStorageSettings.PosterFolder,
                cancellationToken);
        }
        else if (request.PosterUrl != null) 
        {
            // Nếu không up file nhưng truyền URL trực tiếp (giữ nguyên poster hiện có hoặc URL ngoài)
             movie.PosterUrl = request.PosterUrl;
        }

        // Xử lý ngày phát hành
        DateOnly? releaseDate = null;
        if (request.ReleaseDate.HasValue) 
        {
            releaseDate = request.ReleaseDate.Value;
        }

        // Cập nhật các thông tin còn lại của phim
        movie.Title = request.Title;
        movie.DurationMinutes = request.DurationMinutes;
        movie.LanguageId = request.Language?.ToUpperInvariant();
        movie.ReleaseDate = releaseDate;
        movie.AgeRating = request.AgeRating;
        movie.Description = request.Description;
        movie.TrailerUrl = request.TrailerUrl;
        movie.Highlight = request.Highlight;
        movie.Director = request.Director;
        movie.MovieStatus = request.MovieStatus;

        // Nếu có yêu cầu thay đổi thể loại phim
        if (request.GenreIds != null || request.GenreNames != null)
        {
            // Xóa toàn bộ liên kết thể loại cũ
            movie.MovieGenres.Clear();
            // Thêm các liên kết thể loại mới
            foreach (var genreId in finalGenreIds)
            {
                movie.MovieGenres.Add(new MovieGenre { MovieId = movieId, GenreId = genreId });
            }
        }

        // Lưu thay đổi vào CSDL
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Trả về kết quả cập nhật thành công kèm dữ liệu chi tiết phim
        return ServiceResult<MovieDetailResponse>.Ok(ToDetailResponse(movie), "Movie updated successfully.");
    }

    public async Task<ServiceResult<object>> DeleteMovieAsync(
        string movieId,
        string actionUserId,
        CancellationToken cancellationToken)
    {
        // Tìm kiếm bộ phim theo ID
        var movie = await _dbContext.Movies.FirstOrDefaultAsync(m => m.MovieId == movieId, cancellationToken);
        // Nếu không tìm thấy hoặc đã bị xóa mềm (trạng thái Inactive)
        if (movie == null || movie.MovieStatus == DomainConstants.EntityStatus.Inactive)
        {
            // Trả về kết quả không tìm thấy
            return ServiceResult<object>.Fail(404, "Movie not found.", "MOVIE_NOT_FOUND");
        }

        // Tìm các suất chiếu đang mở bán thuộc về bộ phim này
        var openShowtimes = await _dbContext.Showtimes
            .Where(s => s.MovieId == movieId && s.Status == DomainConstants.EntityStatus.Open)
            .Select(s => s.ShowtimeId)
            .ToArrayAsync(cancellationToken);

        // Nếu có suất chiếu đang mở
        if (openShowtimes.Any())
        {
            // Bắt buộc hủy các suất chiếu và hoàn tiền cho khách
            var refundResult = await _refundService.CancelShowtimesAndRefundAsync(openShowtimes, "Movie " + movie.Title + " was deleted.", true, actionUserId, cancellationToken);
            // Nếu hủy suất chiếu không thành công
            if (!refundResult.Success)
            {
                // Trả về lỗi cản trở việc xóa phim
                return ServiceResult<object>.Fail(refundResult.StatusCode, refundResult.Message, refundResult.ErrorCode!);
            }
        }

        // Cập nhật trạng thái phim thành Inactive (Xóa mềm - Soft Delete) thay vì xóa vật lý
        movie.MovieStatus = DomainConstants.EntityStatus.Inactive;
        // Lưu thay đổi xuống Database
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Trả về kết quả xóa mềm thành công
        return ServiceResult<object>.Ok(new { MovieId = movieId, Status = movie.MovieStatus }, "Movie softly deleted successfully.");
    }

    public async Task UpdateMovieRatingAsync(string movieId, int ratingDiff, int reviewCountDiff, CancellationToken cancellationToken)
    {
        // Truy xuất thông tin phim theo ID
        var movie = await _dbContext.Movies.FirstOrDefaultAsync(m => m.MovieId == movieId, cancellationToken);
        // Nếu không tìm thấy phim thì thoát sớm (không làm gì)
        if (movie == null) return;

        // Tính tổng điểm đánh giá hiện tại của phim (điểm trung bình x tổng số lượt đánh giá)
        decimal currentTotalScore = movie.AverageRating * movie.TotalReviews;
        // Tính tổng điểm mới (cộng hoặc trừ điểm chênh lệch từ thay đổi mới)
        decimal newTotalScore = currentTotalScore + ratingDiff;
        
        // Cập nhật lại tổng số lượt đánh giá
        movie.TotalReviews += reviewCountDiff;
        // Tính lại điểm đánh giá trung bình mới (làm tròn 2 chữ số thập phân, gán bằng 0 nếu chưa có đánh giá nào)
        movie.AverageRating = movie.TotalReviews > 0 ? Math.Round(newTotalScore / movie.TotalReviews, 2) : 0;

        // Lưu thay đổi điểm đánh giá vào CSDL
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static readonly HttpClient _httpClient = new HttpClient();

    private static string CleanHtml(string html)
    {
        if (string.IsNullOrEmpty(html)) return string.Empty;

        // Preserve JSON-LD tags (IMDb/TMDB/Wikipedia metadata is kept here)
        var jsonLdBuilder = new System.Text.StringBuilder();
        try
        {
            var jsonLdMatches = System.Text.RegularExpressions.Regex.Matches(
                html,
                @"<script[^>]*type=[""']application/ld\+json[""'][^>]*>([\s\S]*?)</script>",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            foreach (System.Text.RegularExpressions.Match match in jsonLdMatches)
            {
                jsonLdBuilder.AppendLine(match.Value);
            }
        }
        catch { }

        // Strip scripts
        html = System.Text.RegularExpressions.Regex.Replace(html, @"<script[^>]*>[\s\S]*?</script>", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        // Strip styles
        html = System.Text.RegularExpressions.Regex.Replace(html, @"<style[^>]*>[\s\S]*?</style>", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        // Strip svg
        html = System.Text.RegularExpressions.Regex.Replace(html, @"<svg[^>]*>[\s\S]*?</svg>", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        // Strip comments
        html = System.Text.RegularExpressions.Regex.Replace(html, @"<!--[\s\S]*?-->", "");
        // Clean multiple spaces/newlines
        html = System.Text.RegularExpressions.Regex.Replace(html, @"\s+", " ");

        if (jsonLdBuilder.Length > 0)
        {
            html = jsonLdBuilder.ToString() + "\n" + html;
        }

        return html.Length > 50000 ? html.Substring(0, 50000) : html;
    }

    public async Task<ServiceResult<MovieAutofillResponse>> AutofillMovieFromUrlAsync(
        MovieAutofillRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_geminiSettings.ApiKey))
        {
            return ServiceResult<MovieAutofillResponse>.Fail(500, "Gemini API key is not configured.", "MISSING_API_KEY");
        }

        try
        {
            var httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, request.Url);
            httpRequestMessage.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

            using var httpResponse = await _httpClient.SendAsync(httpRequestMessage, cancellationToken);
            if (!httpResponse.IsSuccessStatusCode)
            {
                return ServiceResult<MovieAutofillResponse>.Fail(400, $"Failed to fetch URL: {httpResponse.StatusCode}", "URL_FETCH_ERROR");
            }

            var html = await httpResponse.Content.ReadAsStringAsync(cancellationToken);
            var cleanContent = CleanHtml(html);

            var payload = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = $"Read the following HTML/text from a movie website and extract the movie details, including the poster image URL and trailer video URL if available. Translate movie title, description to Vietnamese if appropriate, but keep the original title as title if it is international. Populate the fields. Website Content:\n\n{cleanContent}" }
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
                            title = new { type = "STRING", description = "The movie title (in Vietnamese or English/Original)" },
                            durationMinutes = new { type = "INTEGER", description = "Duration of movie in minutes" },
                            genres = new { type = "ARRAY", items = new { type = "STRING" }, description = "List of movie genres (e.g. Action, Comedy, Drama, Horror, Sci-Fi)" },
                            language = new { type = "STRING", description = "Primary language of the movie" },
                            releaseDate = new { type = "STRING", description = "Release date of the movie in YYYY-MM-DD format" },
                            ageRating = new { type = "STRING", description = "Age rating of the movie (e.g. P, T13, T16, T18, K)" },
                            description = new { type = "STRING", description = "Plot summary of the movie in Vietnamese" },
                            director = new { type = "STRING", description = "Director name" },
                            trailerUrl = new { type = "STRING", description = "Official trailer URL if present (from meta tags like og:video, JSON-LD, or links)" },
                            posterUrl = new { type = "STRING", description = "Movie poster image URL (from meta tags like og:image, JSON-LD, or links)" }
                        },
                        required = new[] { "title", "durationMinutes" }
                    }
                }
            };

            var url = $"{_geminiSettings.ApiBaseUrl.TrimEnd('/')}/{Uri.EscapeDataString(_geminiSettings.Model)}:generateContent";
            using var geminiRequest = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = JsonContent.Create(payload)
            };
            geminiRequest.Headers.TryAddWithoutValidation(Configuration.GeminiSettings.ApiKeyHeaderName, _geminiSettings.ApiKey);

            using var geminiResponse = await _httpClient.SendAsync(geminiRequest, cancellationToken);
            if (!geminiResponse.IsSuccessStatusCode)
            {
                var error = await geminiResponse.Content.ReadAsStringAsync(cancellationToken);
                return ServiceResult<MovieAutofillResponse>.Fail(500, $"Gemini API error: {geminiResponse.StatusCode} - {error}", "GEMINI_API_ERROR");
            }

            var jsonDoc = await geminiResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
            var replyText = jsonDoc.GetProperty("candidates")[0]
                                   .GetProperty("content")
                                   .GetProperty("parts")[0]
                                   .GetProperty("text").GetString();

            if (string.IsNullOrWhiteSpace(replyText))
            {
                return ServiceResult<MovieAutofillResponse>.Fail(500, "Gemini returned empty response.", "EMPTY_RESPONSE");
            }

            var autofillData = JsonSerializer.Deserialize<MovieAutofillResponse>(replyText, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (autofillData == null)
            {
                return ServiceResult<MovieAutofillResponse>.Fail(500, "Failed to parse movie details from Gemini response.", "PARSE_ERROR");
            }

            // Fallback: If Gemini could not extract a trailer URL, search YouTube for the official trailer
            if (string.IsNullOrWhiteSpace(autofillData.TrailerUrl) && !string.IsNullOrWhiteSpace(autofillData.Title))
            {
                try
                {
                    var youtube = new YoutubeClient();
                    var searchQuery = $"{autofillData.Title} official trailer";
                    await foreach (var searchResult in youtube.Search.GetResultsAsync(searchQuery, cancellationToken))
                    {
                        if (searchResult != null)
                        {
                            autofillData.TrailerUrl = searchResult.Url;
                            break;
                        }
                    }
                }
                catch
                {
                    // Fail silently so it doesn't block the whole autofill if YouTube search fails
                }
            }

            return ServiceResult<MovieAutofillResponse>.Ok(autofillData, "Movie details extracted successfully.");
        }
        catch (Exception ex)
        {
            return ServiceResult<MovieAutofillResponse>.Fail(500, $"An error occurred: {ex.Message}", "INTERNAL_ERROR");
        }
    }
}
