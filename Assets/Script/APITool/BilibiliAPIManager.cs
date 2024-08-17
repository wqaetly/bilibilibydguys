using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net;
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
    public class BilibiliAPIManager : Instance<BilibiliAPIManager>
    {
        public static readonly CookieContainer cookieContainer = new CookieContainer();

        public static HttpClient httpClient =
            new HttpClient(new HttpClientHandler() { CookieContainer = cookieContainer }) { };
        
        public async UniTask<bool> Login(UniTaskCompletionSource ensureQRCode)
        {
            // 展示QRCode
            await QRCodeTool.Show();

            // 等待外部按钮点击
            await ensureQRCode.Task;

            // 正式登陆
            bool loginResult = await QRCodeTool.Login();

            if (!loginResult)
            {
                return loginResult;
            }

            // 只有登陆成功后才继续往后走
            await QRCodeTool.GetUserInfo();

            return true;
        }
        
    }
}