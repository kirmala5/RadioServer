using RadioServer;

var builder = WebApplication.CreateBuilder(args);

var audioRoot = Path.Combine(builder.Environment.ContentRootPath, "wwwroot", "audio");
builder.Services.AddSingleton<StationManager>(sp =>
    new StationManager(sp.GetRequiredService<ILogger<StationManager>>(), audioRoot));

var app = builder.Build();

app.UseStaticFiles();

app.MapGet("/", () => Results.Redirect("/index.html"));

app.MapGet("/stations", (StationManager mgr) =>
{
    return Results.Json(mgr.GetStationsInfo(), options: new()
    {
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
    });
});

app.MapGet("/stream/{station}", async (string station, HttpContext ctx, StationManager mgr) =>
{
    // 1. Проверяем, существует ли станция.
    if (!mgr.TryGetStation(station, out var st))
    {
        ctx.Response.StatusCode = 404;
        await ctx.Response.WriteAsync($"Station '{station}' not found");
        return;
    }

    // 2. MIME-тип аудиопотока.
    ctx.Response.ContentType = "audio/mpeg";
    // 3. Чанковая передача без Content-Length, отключаем буферизацию прокси.
    ctx.Response.Headers["Cache-Control"] = "no-cache";
    ctx.Response.Headers["X-Accel-Buffering"] = "no";

    var logger = ctx.RequestServices.GetRequiredService<ILogger<Program>>();
    var buffer = new byte[8192];
    var ct = ctx.RequestAborted;

    // 4. Регистрируем слушателя на конкретной станции.
    st.AddListener();
    var info = mgr.GetStationsInfo().FirstOrDefault(s => s.Id.Equals(station, StringComparison.OrdinalIgnoreCase));
    logger.LogInformation("Listener connected to station '{Station}' ({0} total)", station, info?.Listeners ?? 0);

    try
    {
        // 5. Цикл потоковой передачи.
        while (!ct.IsCancellationRequested)
        {
            var track = st.GetCurrentTrack();
            if (string.IsNullOrEmpty(track)) break;

            var path = mgr.GetTrackPath(station, track);
            logger.LogDebug("Station '{Station}' streaming track '{Track}'", station, track);

            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
                int bytesRead;
                while ((bytesRead = await fs.ReadAsync(buffer, ct)) > 0 && !ct.IsCancellationRequested)
                {
                    await ctx.Response.Body.WriteAsync(buffer, 0, bytesRead, ct);
                    await ctx.Response.Body.FlushAsync(ct);
                }
            }
            catch (OperationCanceledException)
            {
                // Клиент отключился — выходим из цикла треков.
                break;
            }
            catch (IOException ex)
            {
                logger.LogWarning(ex, "IOException while streaming track '{Track}' on station '{Station}'", track, station);
                break;
            }
        }
    }
    catch (OperationCanceledException)
    {
        // Нормальное отключение клиента.
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Unexpected error streaming station '{Station}'", station);
    }
    finally
    {
        // 6. Освобождаем слушателя.
        st.RemoveListener();
        logger.LogInformation("Listener disconnected from station '{Station}'", station);
    }
});

app.Run();
