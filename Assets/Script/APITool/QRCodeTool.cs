using System.Net.Http;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;
using ZXing;
using ZXing.QrCode;

namespace Script.APITool
{
    public class QRCodeTool
    {
        public static string qrCodeKey;
        
        /// <summary>
        /// 即UID
        /// </summary>
        public static string mid;
        
        public static string img_url, sub_url;

        /// <summary>
        /// 缓存Cookie，用于自动鉴权
        /// </summary>
        public static string cookieFilePath = $"{Application.dataPath}/cookie.txt";

        private static readonly int[] MixinKeyEncTab =
        {
            46, 47, 18, 2, 53, 8, 23, 32, 15, 50, 10, 31, 58, 3, 45, 35, 27, 43, 5, 49, 33, 9, 42, 19, 29, 28, 14, 39,
            12, 38, 41, 13, 37, 48, 7, 16, 24, 55, 40, 61, 26, 17, 0, 1, 60, 51, 30, 4, 22, 25, 54, 21, 56, 59, 6, 63,
            57, 62, 11, 36, 20, 34, 44, 52
        };

        //对 imgKey 和 subKey 进行字符顺序打乱编码
        private static string GetMixinKey(string orig)
        {
            return MixinKeyEncTab.Aggregate("", (s, i) => s + orig[i])[..32];
        }

        public static Dictionary<string, string> EncWbi(Dictionary<string, string> parameters, string imgKey,
            string subKey)
        {
            string mixinKey = GetMixinKey(imgKey + subKey);
            string currTime = DateTimeOffset.Now.ToUnixTimeSeconds().ToString();
            //添加 wts 字段
            parameters["wts"] = currTime;
            // 按照 key 重排参数
            parameters = parameters.OrderBy(p => p.Key).ToDictionary(p => p.Key, p => p.Value);
            //过滤 value 中的 "!'()*" 字符
            parameters = parameters.ToDictionary(
                kvp => kvp.Key,
                kvp => new string(kvp.Value.Where(chr => !"!'()*".Contains(chr)).ToArray())
            );
            // 序列化参数
            string query = new FormUrlEncodedContent(parameters).ReadAsStringAsync().Result;
            //计算 w_rid
            using MD5 md5 = MD5.Create();
            byte[] hashBytes = md5.ComputeHash(Encoding.UTF8.GetBytes(query + mixinKey));
            string wbiSign = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
            parameters["w_rid"] = wbiSign;

            return parameters;
        }

        // 获取最新的 img_key 和 sub_key
        public static async Task<(string, string)> GetWbiKeys()
        {
            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
            httpClient.DefaultRequestHeaders.Referrer = new Uri("https://www.bilibili.com/");

            HttpResponseMessage responseMessage = await httpClient.SendAsync(new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri("https://api.bilibili.com/x/web-interface/nav"),
            });

            JsonNode response = JsonNode.Parse(await responseMessage.Content.ReadAsStringAsync())!;

            string imgUrl = (string)response["data"]!["wbi_img"]!["img_url"]!;
            imgUrl = imgUrl.Split("/")[^1].Split(".")[0];

            string subUrl = (string)response["data"]!["wbi_img"]!["sub_url"]!;
            subUrl = subUrl.Split("/")[^1].Split(".")[0];
            return (imgUrl, subUrl);
        }

        private static Texture2D GenerateQRCode(string url, int width, int height)
        {
            BarcodeWriterPixelData writer = new BarcodeWriterPixelData()
            {
                Format = BarcodeFormat.QR_CODE,
                Options = new QrCodeEncodingOptions
                {
                    Width = width,
                    Height = height
                }
            };

            var pixels = writer.WriteAsColor32(url);

            Texture2D texture = new Texture2D(width, height);
            texture.SetPixels32(pixels.Pixels);
            texture.Apply();

            return texture;
        }

        public static async UniTask Show()
        {
            var qrCode =
                await BilibiliAPIManager.httpClient.GetAsync(
                    new Uri($"https://passport.bilibili.com/x/passport-login/web/qrcode/generate"));
            var qrCodeInfo = await qrCode.Content.ReadAsStringAsync();
            Debug.Log($"QRCode结果：{qrCodeInfo}");
            JsonNode response = JsonNode.Parse(qrCodeInfo)!;

            qrCodeKey = (string)(response["data"]!["qrcode_key"]!);

            GameEntry.GetInstance.img_QRCode.sprite = Sprite.Create(
                GenerateQRCode((string)(response["data"]!["url"]!), 256, 256),
                new Rect(Vector2.zero, new Vector2(256, 256)), new Vector2(0.5f, 0.5f));
        }

        public static async UniTask<bool> Login()
        {
            if (File.Exists(cookieFilePath))
            {
                var cookieInfo = File.ReadAllLines(cookieFilePath);
                foreach (var item in cookieInfo)
                {
                    BilibiliAPIManager.cookieContainer.SetCookies(new Uri("https://www.bilibili.com/"), item);
                }
                
                // 进行一次测试，如果成功说明cookie未过期，如果未成功则需要正常登陆
                if (await GetUserInfo())
                {
                    return true;
                }
            }
            
            var loginRequest =
                await BilibiliAPIManager.httpClient.GetAsync(
                    new Uri($"https://passport.bilibili.com/x/passport-login/web/qrcode/poll?qrcode_key={qrCodeKey}"));
            var qrCodeInfo = await loginRequest.Content.ReadAsStringAsync();
            JsonNode response = JsonNode.Parse(qrCodeInfo)!;

            Debug.Log($"登陆结果：{qrCodeInfo}");

            if (response["data"]!["code"]!.ToString() == "0")
            {
                Debug.Log($"登录成功：{qrCodeInfo}");

                List<string> cookieInfo = new List<string>();

                foreach (var header in loginRequest.Headers.GetValues("Set-Cookie"))
                {
                    cookieInfo.Add(header);
                    Debug.Log($"设置Cookie：{header}");
                    BilibiliAPIManager.cookieContainer.SetCookies(new Uri("https://www.bilibili.com/"), header);
                }

                File.WriteAllLines(cookieFilePath, cookieInfo);

                return true;
            }
            else
            {
                Debug.Log($"登录失败：{qrCodeInfo}");
                return false;
            }
        }

        public static async UniTask<bool> GetUserInfo()
        {
            var userInfoRequest =
                await BilibiliAPIManager.httpClient.GetAsync("https://api.bilibili.com/x/web-interface/nav");
            var userInfo = await userInfoRequest.Content.ReadAsStringAsync();
            JsonNode response = JsonNode.Parse(userInfo)!;

            string code = response["code"]!.ToString();

            if (code != "0")
            {
                Debug.Log($"鉴权失败，可能是cookie已过期，请重新登录");
                return false;
            }
            
            mid = response["data"]!["mid"]!.ToString();
            img_url = response["data"]!["wbi_img"]!["img_url"]!.ToString();
            sub_url = response["data"]!["wbi_img"]!["sub_url"]!.ToString();

            return true;
        }
    }
}