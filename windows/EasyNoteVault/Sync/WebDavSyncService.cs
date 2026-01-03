using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace EasyNoteVault.Sync;

public class WebDavSyncService
{
    private readonly string _baseUrl;
    private readonly HttpClient _client;

    public WebDavSyncService(string baseUrl, string username, string password)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _client = new HttpClient();

        var auth = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{username}:{password}"));

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", auth);
    }

    private string RemoteFileUrl => $"{_baseUrl}/data.enc";

    // 上传本地 data.enc
    public async Task UploadAsync(string localPath)
    {
        if (!File.Exists(localPath))
            return;

        var bytes = await File.ReadAllBytesAsync(localPath);
        using var content = new ByteArrayContent(bytes);
        content.Headers.ContentType =
            new MediaTypeHeaderValue("application/octet-stream");

        await _client.PutAsync(RemoteFileUrl, content);
    }

    // 下载云端 data.enc
    public async Task DownloadAsync(string localPath)
    {
        var response = await _client.GetAsync(RemoteFileUrl);
        if (!response.IsSuccessStatusCode)
            return;

        var bytes = await response.Content.ReadAsByteArrayAsync();
        await File.WriteAllBytesAsync(localPath, bytes);
    }

    // 获取云端修改时间（用于判断新旧）
    public async Task<DateTime?> GetRemoteLastModifiedAsync()
    {
        var request = new HttpRequestMessage(HttpMethod.Head, RemoteFileUrl);
        var response = await _client.SendAsync(request);

        if (!response.IsSuccessStatusCode)
            return null;

        return response.Content.Headers.LastModified?.UtcDateTime;
    }
}
