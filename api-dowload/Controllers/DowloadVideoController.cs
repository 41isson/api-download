using Microsoft.AspNetCore.Mvc;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using YoutubeExplode;
using YoutubeExplode.Videos.Streams;

namespace api_dowload.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class VideoController : ControllerBase
    {
        private readonly YoutubeClient _youtubeClient;

        public VideoController()
        {
            _youtubeClient = new YoutubeClient();
        }

        private string GetVideoIdFromUrl(string url)
        {
            try
            {
                var uri = new Uri(url);
                var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
                return query["v"] ?? uri.Segments.LastOrDefault()?.Split('/').LastOrDefault();
            }
            catch
            {
                return null;
            }
        }

        [HttpPost("download")]
        public async Task<IActionResult> Download([FromQuery] string videoUrl, [FromQuery] string format = "video")
        {
            if (string.IsNullOrEmpty(videoUrl))
            {
                return BadRequest("Video URL is required.");
            }

            try
            {
                var videoId = GetVideoIdFromUrl(videoUrl);
                if (videoId == null)
                {
                    return BadRequest("Invalid video URL.");
                }

                var video = await _youtubeClient.Videos.GetAsync(videoId);
                var streamManifest = await _youtubeClient.Videos.Streams.GetManifestAsync(videoId);

                IStreamInfo streamInfo;
                string fileName;

                if (format == "audio")
                {
                    var audioStreams = streamManifest.GetAudioOnlyStreams().ToList();
                    streamInfo = audioStreams.OrderByDescending(s => s.Bitrate).FirstOrDefault();
                    fileName = $"{video.Title}.mp3";
                }
                else
                {
                    var videoStreams = streamManifest.GetVideoStreams().ToList();
                    streamInfo = videoStreams
                        .Where(s => s.Container == Container.Mp4)
                        .OrderByDescending(s => s.VideoQuality)
                        .FirstOrDefault();
                    fileName = $"{video.Title}.mp4";
                }

                if (streamInfo == null)
                {
                    return NotFound("No suitable stream found.");
                }

                var stream = await _youtubeClient.Videos.Streams.GetAsync(streamInfo);
                var tempFilePath = Path.GetTempFileName();

                using (var fileStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write))
                {
                    await stream.CopyToAsync(fileStream);
                }

                var fileBytes = await System.IO.File.ReadAllBytesAsync(tempFilePath);
                System.IO.File.Delete(tempFilePath);

                if (fileBytes.Length == 0)
                {
                    throw new Exception("Downloaded file is empty.");
                }

                var contentType = format == "audio" ? "audio/mpeg" : "video/mp4";

                return File(fileBytes, contentType, fileName);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }
    }
}
