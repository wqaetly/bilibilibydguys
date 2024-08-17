using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json.Nodes;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Script.APITool
{
    /// <summary>
    /// 评论区工具
    /// </summary>
    public class ReplyTool
    {
        public static string replayInfoFilePath = $"{Application.dataPath}/replayInfo.txt";

        /// <summary>
        /// 获取有效用户数量（去重），同时会将mid写入一个文件中
        /// </summary>
        /// <returns></returns>
        public async static UniTask<int> GetValidUser()
        {
            int currentPageIndex = 0;
            bool hasArrivedLast = false;

            Dictionary<string, string> uidInfo = new Dictionary<string, string>();

            while (!hasArrivedLast)
            {
                // 等待1s进行下一个请求
                await UniTask.Delay(1000, DelayType.DeltaTime);

                var userInfoRequest =
                    await BilibiliAPIManager.httpClient.GetAsync(
                        $"https://api.bilibili.com/x/v2/reply?oid={GameEntry.GetInstance.oid}&type={GameEntry.GetInstance.replyType}&pn={currentPageIndex}");
                var userInfo = await userInfoRequest.Content.ReadAsStringAsync();
                JsonNode response = JsonNode.Parse(userInfo)!;

                if (response["code"]!.ToString() != "0")
                {
                    hasArrivedLast = true;
                    currentPageIndex--;
                    GameEntry.GetInstance.AddItem($"中断拉取，拉取评论页{currentPageIndex}时发现已无更多评论可拉取，正式开始抽奖：{response["message"]!}");
                    Debug.LogError($"中断拉取，拉取评论页{currentPageIndex}时出现异常：{response["message"]!}");
                }
                else
                {
                    GameEntry.GetInstance.AddItem($"评论拉取成功，当前已拉取大约 {(currentPageIndex + 1) * 20} 条评论");
                    Debug.Log($"评论拉取成功：{userInfo}");

                    // 正式处理数据
                    var array = response["data"]!["replies"]!.AsArray();

                    foreach (var replayItem in array)
                    {
                        if (replayItem != null)
                        {
                            uidInfo[replayItem["member"]!["mid"]!.ToString()] =
                                replayItem["member"]!["uname"]!.ToString();
                        }
                    }

                    currentPageIndex++;
                }
            }

            List<string> luckyGuy = new List<string>();
            List<string> logInfo = new List<string>();
            foreach (var uidItem in uidInfo)
            {
                luckyGuy.Add(uidItem.Key);
                logInfo.Add($"uid: {uidItem.Key} 昵称：{uidItem.Value}");
            }

            HashSet<string> finalLuckyGuy = new HashSet<string>();

            while (finalLuckyGuy.Count < 10)
            {
                await UniTask.Delay(5000);
                
                logInfo.Add("开始抽取幸运儿。。。");
                GameEntry.GetInstance.AddItem("开始抽取幸运儿。。。");

                // 随机数得出结果
                int randNum = Random.Range(0, luckyGuy.Count);

                logInfo.Add(
                    $"{DateTime.Now} 随机数为：{randNum} 序号为：{randNum}，抽取到：{luckyGuy[randNum]}，检测其是否符合资格（点赞，评论，关注我）。。");

                GameEntry.GetInstance.AddItem($"随机数为：<color=red>{randNum}</color> 抽取到用户：<color=red>{uidInfo[luckyGuy[randNum]]}</color>，检测其是否拥有资格。。");

                await UniTask.Delay(5000);
                
                var (imgKey, subKey) = await QRCodeTool.GetWbiKeys();

                Dictionary<string, string> signedParams = QRCodeTool.EncWbi(
                    parameters: new Dictionary<string, string>
                    {
                        { "mid", luckyGuy[randNum] },
                    },
                    imgKey: imgKey,
                    subKey: subKey
                );

                string query = await new FormUrlEncodedContent(signedParams).ReadAsStringAsync();

                var relationShip =
                    await BilibiliAPIManager.httpClient.GetAsync(
                        $"https://api.bilibili.com/x/space/wbi/acc/relation?{query}");
                var userInfo = await relationShip.Content.ReadAsStringAsync();

                JsonNode response = JsonNode.Parse(userInfo)!;

                if (response["code"]!.ToString() != "0")
                {
                    logInfo.Add($"用户非法，跳过");
                    GameEntry.GetInstance.AddItem($"<color=red>用户非法，跳过</color>");
                    continue;
                }

                if (response["data"]!["be_relation"]!["attribute"]!.ToString() != "2" &&
                    response["data"]!["be_relation"]!["attribute"]!.ToString() != "6")
                {
                    logInfo.Add($"用户未关注我，跳过");
                    GameEntry.GetInstance.AddItem($"<color=red>用户未关注我，跳过</color>");
                    continue;
                }

                if (finalLuckyGuy.Contains(luckyGuy[randNum]))
                {
                    logInfo.Add($"用户已中奖，被二次选中了。。。真牛逼，跳过");
                    GameEntry.GetInstance.AddItem($"<color=green>用户已中奖，被二次选中了。。。真牛逼，跳过</color>");
                    continue;
                }

                finalLuckyGuy.Add(luckyGuy[randNum]);

                logInfo.Add($"用户符合条件，恭喜中奖！");
                GameEntry.GetInstance.AddItem($"<color=red>用户符合条件，恭喜中奖！</color>");
            }

            logInfo.Add($"所有评论拉取完成，页数{currentPageIndex}，且已得出所有中奖用户");
            GameEntry.GetInstance.AddItem($"所有评论拉取完成，页数{currentPageIndex}，且已得出所有中奖用户");

            GameEntry.GetInstance.AddItem($"本次活动所有中奖名单如下：");
            foreach (var userInfo in finalLuckyGuy)
            {
                logInfo.Add($"uid: {userInfo} 昵称：{uidInfo[userInfo]}");
                GameEntry.GetInstance.AddItem($"uid: <color=red>{userInfo}</color> 昵称：<color=red>{uidInfo[userInfo]}</color>");
            }

            await File.WriteAllLinesAsync(replayInfoFilePath, logInfo);

            Debug.Log($"所有评论拉取完成，页数{currentPageIndex}，且已得出所有中奖用户");
            GameEntry.GetInstance.AddItem($"日志文件已写入到本地的{replayInfoFilePath}下");
            
            return 0;
        }
    }
}