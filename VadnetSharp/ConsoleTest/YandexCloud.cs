using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ConsoleTest
{
    public class YandexCloud
    {
        private readonly HttpClient _client = new HttpClient();

        private string _token;

        public YandexCloud()
        {
        }

        public void RefreshToken(string oauth)
        {
            var result = _client.PostAsync(
                "https://iam.api.cloud.yandex.net/iam/v1/tokens",
                new StringContent($"{{\"yandexPassportOauthToken\": \"{oauth}\"}}")).Result;

            string content = result.Content.ReadAsStringAsync().Result;
            var jobj = JObject.Parse(content);
            _token = jobj["iamToken"].ToString();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        }

        public string RecognizeText(byte[] data)
        {
            try
            {
                var content = new ByteArrayContent(data);
                var result = _client.PostAsync(
                    "https://stt.api.cloud.yandex.net/speech/v1/stt:recognize?format=oggopus&folderId=b1gb3vklrrgek5gejhbv",
                    content).Result;


                string rcontent = result.Content.ReadAsStringAsync().Result;
                var jobj = JObject.Parse(rcontent);
                return jobj["result"].ToString();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                return "";
            }
        }
    }
}