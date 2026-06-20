using System.Windows.Media;
using System.Windows.Media.Imaging;
using FFmpeg.AutoGen;

namespace OpenAdoration.WPF.Helpers;

/// <summary>
/// Decodes a single frame from a video via the already-loaded FFmpeg libraries (FFmpeg.AutoGen),
/// for codecs Windows can't thumbnail itself (notably HEVC/H.265 iPhone .MOV). Returns a frozen
/// <see cref="BitmapSource"/> or null on any failure — callers treat null as "no thumbnail".
/// Slow (decodes a frame), so run it off the UI thread and cache the result.
/// </summary>
internal static unsafe class FfmpegThumbnail
{
    public static BitmapSource? TryExtract(string path, int maxSize)
    {
        if (!MediaEngine.IsLoaded) return null;

        AVFormatContext* fmt = null;
        AVCodecContext* codec = null;
        AVFrame* frame = null;
        AVPacket* packet = null;
        SwsContext* sws = null;
        byte* dst = null;

        try
        {
            if (ffmpeg.avformat_open_input(&fmt, path, null, null) != 0) return null;
            if (ffmpeg.avformat_find_stream_info(fmt, null) < 0) return null;

            AVCodec* decoder = null;
            var stream = ffmpeg.av_find_best_stream(fmt, AVMediaType.AVMEDIA_TYPE_VIDEO, -1, -1, &decoder, 0);
            if (stream < 0 || decoder == null) return null;

            codec = ffmpeg.avcodec_alloc_context3(decoder);
            if (codec == null) return null;
            if (ffmpeg.avcodec_parameters_to_context(codec, fmt->streams[stream]->codecpar) < 0) return null;
            if (ffmpeg.avcodec_open2(codec, decoder, null) < 0) return null;

            frame = ffmpeg.av_frame_alloc();
            packet = ffmpeg.av_packet_alloc();
            if (frame == null || packet == null) return null;

            if (!DecodeFirstFrame(fmt, codec, packet, frame, stream)) return null;

            // Scale to a thumbnail (preserve aspect) and convert to BGRA32 for WPF.
            var (w, h) = FitInside(codec->width, codec->height, maxSize);
            sws = ffmpeg.sws_getContext(codec->width, codec->height, codec->pix_fmt,
                w, h, AVPixelFormat.AV_PIX_FMT_BGRA, ffmpeg.SWS_BILINEAR, null, null, null);
            if (sws == null) return null;

            var stride = w * 4;
            dst = (byte*)ffmpeg.av_malloc((ulong)(stride * h));
            var dstData = new byte_ptrArray4 { [0] = dst };
            var dstLines = new int_array4 { [0] = stride };
            ffmpeg.sws_scale(sws, frame->data, frame->linesize, 0, codec->height, dstData, dstLines);

            var bmp = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null,
                (IntPtr)dst, stride * h, stride);
            bmp.Freeze();
            return bmp;
        }
        catch
        {
            return null;
        }
        finally
        {
            if (dst != null) ffmpeg.av_free(dst);
            if (sws != null) ffmpeg.sws_freeContext(sws);
            if (packet != null) ffmpeg.av_packet_free(&packet);
            if (frame != null) ffmpeg.av_frame_free(&frame);
            if (codec != null) ffmpeg.avcodec_free_context(&codec);
            if (fmt != null) ffmpeg.avformat_close_input(&fmt);
        }
    }

    private static bool DecodeFirstFrame(AVFormatContext* fmt, AVCodecContext* codec,
        AVPacket* packet, AVFrame* frame, int stream)
    {
        while (ffmpeg.av_read_frame(fmt, packet) >= 0)
        {
            try
            {
                if (packet->stream_index != stream) continue;
                if (ffmpeg.avcodec_send_packet(codec, packet) < 0) return false;
                var r = ffmpeg.avcodec_receive_frame(codec, frame);
                if (r == 0) return true;                 // got a frame
                if (r == ffmpeg.AVERROR(ffmpeg.EAGAIN)) continue; // needs more packets
                return false;
            }
            finally
            {
                ffmpeg.av_packet_unref(packet);
            }
        }
        return false;
    }

    private static (int w, int h) FitInside(int w, int h, int max)
    {
        if (w <= 0 || h <= 0) return (max, max);
        if (w <= max && h <= max) return (w, h);
        var scale = Math.Min((double)max / w, (double)max / h);
        return (Math.Max(1, (int)(w * scale)), Math.Max(1, (int)(h * scale)));
    }
}
