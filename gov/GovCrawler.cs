﻿using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Remoting.Messaging;
using System.Security.Policy;
using System.Web;
using HtmlAgilityPack;

namespace gov
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Text.RegularExpressions;
    using GsxtWebCore;
    using Newtonsoft.Json.Linq;
    using X.CommLib.Logs;


    /// <summary>
    /// </summary>
    /// <seealso cref="X.CommLib.Logs.NormalLogginger" />
    public sealed class GovCrawler : NormalLogginger
    {
        /// <summary>
        ///     The HTTP helper
        /// </summary>
        private readonly HttpHelper _httpHelper;

        /// <summary>
        ///     The key word
        /// </summary>
        private string _keyWord;


        /// <summary>
        /// _html
        /// </summary>
        private string _html;

        /// <summary>
        ///     GovCrawler
        /// </summary>
        public GovCrawler()
        {
            this._httpHelper = new HttpHelper();
        }

        /// <summary>
        ///     GetCompanyInfoDicByKeyWord
        /// </summary>
        /// <param name="keyWord"></param>
        /// <returns></returns>
        public Dictionary<string, string> GetCompanyInfoDicByKeyWord(string keyWord)
        {
            _html = this.SlideIntoGetHtml(this._keyWord = keyWord);
            return this.GetCompanyInfoDic(_html);
        }


        public List<Dictionary<string, string>> GetPunishmentDetailInfoDicList(string companyName)
        {
            return GetPunishmentDetailInfoDicList(companyName,_html);
        }

        /// <summary>
        ///     GetAlterInfoString
        /// </summary>
        /// <param name="html"></param>
        /// <returns></returns>
        private string GetAlterInfoString(string html)
        {
            Func<string, string> convertToDateTime = s =>
            {
                var startTime = TimeZone.CurrentTimeZone.ToLocalTime(new DateTime(1970, 1, 1));
                var dateTime = startTime.AddMilliseconds(double.Parse(s));
                return dateTime.ToString("yyyyMMdd");
            };

            Func<string, string> removeUseless = s =>
            {
                s = Regex.Replace(s, "<span class=.*?>.*?</span>", string.Empty);
                return Regex.Replace(s, "<div class=.*?>.*?</div>", string.Empty);
            };

            var alterInfoUrl = Regex.Match(html, "(?<=alterInfoUrl = \").*?(?=\")").Value;
            alterInfoUrl = $"http://www.gsxt.gov.cn{alterInfoUrl}";
            var alterHtml = this._httpHelper.GetHtmlByGet(alterInfoUrl);
            var jObject = JObject.Parse(alterHtml);
            var jToken = jObject["data"];
            if (jToken == null)
            {
                throw new Exception("json解析失败，jToken为空。");
            }

            var jArray = JArray.Parse(jToken.ToString());
            var result = string.Empty;
            foreach (var token in jArray)
            {
                var alterDate = convertToDateTime(token["altDate"].ToString());

                var altItemCn = removeUseless(token["altItem_CN"].ToString());

                result += $"{alterDate}^^^{altItemCn}|||";
            }

            return result;
        }


        /// <summary>
        /// GetMsgTitle
        /// </summary>
        /// <param name="html"></param>
        /// <returns></returns>
        private string GetMsgTitle(string html)
        {
            return Regex.Match(html, @"(?<=<div class=""msgTitle""[^>]*?>[\s]*)[\S]*?(?=[\s]*<div)").Value;
        }

        /// <summary>
        /// GetCompanyInfoDic
        /// </summary>
        /// <param name="html"></param>
        /// <returns></returns>
        private Dictionary<string, string> GetCompanyInfoDic(string html)
        {

            /* 
               //表示整篇Html文档，就算选了Node还是整篇文档找，这里有点坑
               /和./表示当前选择的Node开始找 
               @表示属性
            */
            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            var rootNode = doc.DocumentNode;
            var primaryInfoNode = rootNode.SelectSingleNode(@"//div[@id='primaryInfo']");
            var infoNode = primaryInfoNode.SelectNodes(".//dl");
            var dic = new Dictionary<string, string>();
            foreach (var info in infoNode)
            {
                var key = info.SelectSingleNode(".//dt").InnerText;
                //Console.WriteLine($"key:{key}");
                key = ConvertName(key);
                //如果key为空 不要这个值
                if (string.IsNullOrEmpty(key))
                    continue;
                var value = info.SelectSingleNode(".//dd").InnerText;
                //Console.WriteLine($"value:{value}");
                value = FormatValue(key, value);
                //不存在 加入字典
                if (!dic.ContainsKey(key))
                    dic.Add(key, value);
            }

            //var xPathNavigator = HtmlDocumentHelper.CreateNavigator(html);
            //var infoNode = xPathNavigator.SelectSingleNode(@"//div[@id='primaryInfo']");
            //var iterator = infoNode.Select(@".//dl");
            //var list = new List<string>();
            //foreach (XPathNavigator dlnode in iterator)
            //{
            //    // var key = dlnode.SelectSingleNode(@"dt")?.Value.Trim();
            //    var value = dlnode.SelectSingleNode(@"dd")?.Value.Trim();
            //    list.Add(value);
            //}

            //if (list.Count != 13) throw new Exception($"{this._keyWord}:新的html格式。");

            // 统一社会信用代码
            // 企业名称
            // 类型
            // 法定代表人
            // 注册资本
            // 成立时间
            // 营业期限自
            // 营业期限至
            // 登记机关
            // 核准日期
            // 登记状态
            // 住所
            // 经营范围

            //var dic = new Dictionary<string, string>
            //{
            //    ["id"] = getId(list[0]),
            //    ["companyName"] = list[1],
            //    ["type"] = list[2],
            //    ["legalRepresentative"] = list[3],
            //    ["registeredCapital"] = removeSpace(list[4]),
            //    ["setupTime"] = list[5],
            //    ["operationPeriodFrom"] = list[6],
            //    ["operationPeriodTo"] = list[7],
            //    ["registrationAuthority"] = list[8],
            //    ["approvedDate"] = list[9],
            //    ["registrationStatus"] = list[10],
            //    ["location"] = list[11],
            //    ["businessScope"] = removeSpace(list[12])
            //};

            // 变更信息
            dic.Add("alterInfo", GetAlterInfoString(html));
            // 经营异常信息 
            dic.Add("msgTitle", GetMsgTitle(html));


           


            return dic;
        }


        private List<Dictionary<string, string>> GetPunishmentDetailInfoDicList(string companyName,string html)
        {

            Func<string, string> formatDateTime = s =>
            {
                if (string.IsNullOrEmpty(s))
                    return null;
                //s = s.Substring(0, s.Length - 3);

                DateTime startUnixTime = System.TimeZoneInfo.ConvertTime(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc), TimeZoneInfo.Local);
                DateTime time = startUnixTime.AddMilliseconds(double.Parse(s));
                return time.ToString("d");
            };

            List<Dictionary<string,string>> dicList = new List<Dictionary<string, string>>();
            //行政处罚信息
            string punishmentDetailInfoUrl = $"http://www.gsxt.gov.cn{Regex.Match(html, "(?<=punishmentDetailInfoUrl = \").*(?=\")").Value}";
            string punishmentDetailInfoContent = _httpHelper.GetHtmlByGet(punishmentDetailInfoUrl);
            

            JObject jObject = JObject.Parse(punishmentDetailInfoContent);
            JArray jArray = JArray.Parse(jObject["data"].ToString());

            foreach (JToken jToken in jArray)
            {
                //案件编号
                string caseId = jToken["caseId"].ToString();
                //决定书文号
                string penDecNo = jToken["penDecNo"]?.ToString();
                //违法行为类型
                string illegActType = jToken["illegActType"]?.ToString();
                //行政处罚内容
                string penContent = jToken["penContent"]?.ToString();
                //决定机关名称
                string penAuth_CN = jToken["penAuth_CN"]?.ToString();
                //处罚决定日期
                string penDecIssDate = formatDateTime(jToken["penDecIssDate"]?.ToString());
                //公示日期
                string publicDate = formatDateTime(jToken["publicDate"]?.ToString());
                //所有数据
                string json = jToken.ToString();

                Dictionary<string,string> dic = new Dictionary<string, string>
                {
                    ["companyName"] = companyName,
                    ["caseId"] = caseId,
                    ["penDecNo"] = penDecNo,
                    ["illegActType"] = illegActType,
                    ["penContent"] = penContent,
                    ["penAuth_CN"] = penAuth_CN,
                    ["penDecIssDate"] = penDecIssDate,
                    ["publicDate"] = publicDate,
                    ["json"] = json
                };
                
                dicList.Add(dic);
            }            

            return dicList;
        }


        void Test2()
        {

            Func<string, string> formatDateTime = s =>
            {
                if (string.IsNullOrEmpty(s))
                    return null;
                //s = s.Substring(0, s.Length - 3);

                DateTime startUnixTime = System.TimeZoneInfo.ConvertTime(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc), TimeZoneInfo.Local);
                DateTime time = startUnixTime.AddMilliseconds(double.Parse(s));
                return time.ToString("d");
            };


            string punishmentDetailInfoContent =
                @"{'cacheKey':'0_5','currentPage':0,'data':[{'alt':'','altDate':null,'alt_penAuth_CN':'','caseId':'4FA2CB3855C5D170E050000A0CC8077A','gtRegNo':'','illegActType':'','leRep':'','name':'','nodeNum':'340000','penAuth_CN':'','penContent':'','penDecIssDate':null,'penDecNo':'省直地税〔罚〕20164号','publicDate':null,'regNo':'340000000000848','type':'2','uniscId':'913407001489736421','unitName':'','vPunishmentAltInfo':[],'vPunishmentDecision':null},{'alt':'','altDate':null,'alt_penAuth_CN':'','caseId':'5867A03EAB59E188E050007F01005B70','gtRegNo':'','illegActType':'','leRep':'','name':'','nodeNum':'340000','penAuth_CN':'','penContent':'','penDecIssDate':1472609078000,'penDecNo':'省直地税〔罚〕20164号','publicDate':null,'regNo':'340000000000848','type':'2','uniscId':'913407001489736421','unitName':'','vPunishmentAltInfo':[],'vPunishmentDecision':null}],'draw':1,'error':'','perPage':5,'recordsFiltered':2,'recordsTotal':2,'start':0,'totalPage':1}";
            JObject jObject = JObject.Parse(punishmentDetailInfoContent);
            JArray jArray = JArray.Parse(jObject["data"].ToString());

            foreach (JToken jToken in jArray)
            {
                //案件编号
                string caseId = jToken["caseId"].ToString();
                //决定书文号
                string penDecNo = jToken["penDecNo"]?.ToString();
                //违法行为类型
                string illegActType = jToken["illegActType"]?.ToString();
                //行政处罚内容
                string penContent = jToken["penContent"]?.ToString();
                //决定机关名称
                string penAuth_CN = jToken["penAuth_CN"]?.ToString();
                //处罚决定日期
                string penDecIssDate = formatDateTime(jToken["penDecIssDate"]?.ToString());
                //公示日期
                string publicDate = formatDateTime(jToken["publicDate"]?.ToString());
                //所有数据
                string data = jToken.ToString();

            }
        }


        ///// <summary>
        /////     GetMainHtml
        ///// </summary>
        ///// <param name="html"></param>
        ///// <returns></returns>
        //private string GetMainHtml(string html)
        //{
        //    var url = this.GetMainUrl(html);
        //    return string.IsNullOrEmpty(url) ? string.Empty : this._httpHelper.GetHtmlByGet(url);
        //}

        /// <summary>
        /// GetMainHtml
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        private string GetMainHtml(string url)
        {
            return this._httpHelper.GetHtmlByGet(url);
        }

        /// <summary>
        /// GetMainUrl
        /// </summary>
        /// <param name="html"></param>
        /// <returns></returns>
        private string GetMainUrl(string html)
        {
            string info = Regex.Match(html, "(?<=<p class=\"prom\">).*(?=</p>)").Value;
            if(info.Equals("由于您操作过于频繁，请稍后返回首页重新操作"))
                throw new Exception("由于您操作过于频繁，请稍后返回首页重新操作");

            var matches = Regex.Matches(html, @"<a class=""search_list_item db""[\s\S]*?</a>");
            if(matches.Count==0)
                throw new CompanyNotFoundException();
            foreach (Match match in matches)
            {
                var value = match.Value;
                var keyWords = Regex.Matches(value, "(?<=<font color=\"red\">).*?(?=</font>)");
                foreach (Match keyWord in keyWords)
                {
                    if (keyWord.Value.Equals(this._keyWord))
                    {
                        var url = Regex.Match(value, @"(?<=<a class=""search_list_item db""[\s\S]*?href="").*?(?="">)").Value;
                        return $"http://www.gsxt.gov.cn{url}";
                    }
                }
                
            }

            throw new CompanyNotFoundException();
        }

      

        /// <summary>
        ///     SlideIntoGetHtml
        /// </summary>
        /// <param name="keyWord"></param>
        /// <returns></returns>
        private string SlideIntoGetHtml(string keyWord)
        {
            var browser = new Browser();
            browser.OnLogginEvent += this.PostLoggingMessage;

            try
            {

                // _keyWord = @"东易日盛家居装饰集团股份有限公司";

                //var getCaptchaParser = new GtCaptchaParser();
                //browser.GtCaptchaParser = getCaptchaParser;
                //var dragTraceCreator = new DragTraceCreator();
                //browser.DragTraceCreator = dragTraceCreator;

                //var url = browser.SearchCompanyUrl(keyWord);
                //var content = GetMainHtml(url);


                var content = SlideHandler.GetHtml(keyWord);
                var url = GetMainUrl(content);
                var httpHelper = new HttpHelper();
                content = httpHelper.GetHtmlByGet(url);

                return content;
            }
            finally
            {
                browser.OnLogginEvent -= this.PostLoggingMessage;
            }
        }


        
        /// <summary>
        /// FormatValue
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        private string FormatValue(string key, string value)
        {
            Func<string, string> removeSpace = s => s.Trim();
            Func<string, string> getId = s => Regex.Match(s, @"\d+\w+").Value;
            if (key.Equals("registeredCapital") || key.Equals("businessScope"))
            {
                return removeSpace(value);
            }
            else if (key.Equals("id"))
            {
                return getId(value);
            }
            else
            {
                return value;
            }
        }

        /// <summary>
        /// ConvertName
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        private string ConvertName(string name)
        {
            if (name.Contains("统一社会信用代码") || name.Contains("注册号"))
            {
                return "id";
            }
            else if (name.Contains("企业名称") || name.Contains("名称"))
            {
                return "companyName";
            }
            else if (name.Contains("类型"))
            {
                return "type";
            }
            else if (name.Contains("法定代表人") || name.Contains("执行事务合伙人") || name.Contains("投资人") || name.Contains("经营者"))
            {
                return "legalRepresentative";
            }
            else if (name.Contains("注册资本"))
            {
                return "registeredCapital";
            }
            else if (name.Contains("成立时间") || name.Contains("注册日期"))
            {
                return "setupTime";
            }
            else if (name.Contains("营业期限自") || name.Contains("合伙期限自"))
            {
                return "operationPeriodFrom";
            }
            else if (name.Contains("营业期限至") || name.Contains("合伙期限至"))
            {
                return "operationPeriodTo";
            }
            else if (name.Contains("登记机关"))
            {
                return "registrationAuthority";
            }
            else if (name.Contains("核准日期"))
            {
                return "approvedDate";
            }
            else if (name.Contains("登记状态"))
            {
                return "registrationStatus";
            }
            else if (name.Contains("住所") || name.Contains("主要经营场所"))
            {
                return "location";
            }
            else if (name.Contains("经营范围"))
            {
                return "businessScope";
            }
            //else if (name.Contains("组成形式"))
            //{
            //    return "compositionForm";
            //}
            else
            {
                return string.Empty;
            }
        }

        private void Test()
        {
            string s = Path.GetTempPath();

            // Func<string, string> removeUseless = s => Regex.Replace(s, "<span class=.*?>.*?</span>", "");
            // var value = removeUseless("住所(营<span class=\"dp\">5L2P5omAKOiQpeS4muWcuuaJgOOAgeWcsOWdgA==</span>业场所、地址)变<span class=\"dp\">5L2P5omAKOiQpeS4muWcuuaJgOOAgeWcsOWdgA==</span>更");
            //银座集团股份有限公司
            // 东易日盛家居装饰集团股份有限公司
            this._keyWord = "环宇集团浙江高科有限公司";
            var html = this.SlideIntoGetHtml(this._keyWord);

            // html = GetMainHtml(html);
            var companyInfo = this.GetCompanyInfoDic(html);
            foreach (var info in companyInfo)
            {
                Console.WriteLine($"{info.Key}:{info.Value}");
            }
        }


        void Test1()
        {
            HttpHelper httpHelper = new HttpHelper();
            httpHelper.GetHtmlByGet("http://www.gsxt.gov.cn/%7BZfMdwzW4u0iAlfiNKds1ckugpy6rYk_0p6tP_ocIugXGBnhQmdWi7fL8z_4_lC23YES-y9NDNUTqlFUOSjUkvlqdvSkPyrCSrRYYE8ia9f2KPn3hCxx7EvJCsMj8tcjo-1510218554887%7D");

        }
    }

    /// <summary>
    /// GtCaptchaParser
    /// </summary>
    public class GtCaptchaParser : IGtCaptchaParser
    {
        /// <summary>
        ///     CountDistance
        /// </summary>
        /// <param name="orgImage"></param>
        /// <param name="secondImage"></param>
        /// <returns></returns>
        public int CountDistance(Image orgImage, Image secondImage)
        {
            return SlideImageHandler.FindXDiffRectangeOfTwoImage(orgImage, secondImage) - 7;
        }
    }



    /// <summary>
    /// DragTraceCreator
    /// </summary>
    public class DragTraceCreator : IDragTraceCreator
    {
        public TracePoint[] Count(int distance)
        {
            var random = new Random();

            var curDistance = 0;
            var totalSleepTime = 0D;
            var listX = new List<int>();
            var listY = new List<int>();
            var listSleepTime = new List<double>();

            //先加一个初始点
            listX.Add(0);
            listY.Add(random.Next(-2, 2));
            listSleepTime.Add(NextDouble(random, 10, 50));


            //curDistance = curDistance + curDistance + random.Next(1, 5);
            while (Math.Abs(distance - curDistance) > 1)
            {
                //模拟加速的一个过程 这是段神奇的代码
                var moveX = curDistance + random.Next(1, 5);
                var moveY = random.Next(-2, 2);
                var sleepTime = NextDouble(random, 10, 50);
                listX.Add(moveX);
                listY.Add(moveY);
                listSleepTime.Add(sleepTime);
                curDistance += moveX;
                totalSleepTime += sleepTime;
                //如果当前的距离大于等于给的距离退出
                if (curDistance >= distance)
                    break;
            }

            //如果移过头了 最后终点加入
            if (curDistance > distance)
            {
                listX.Add(distance);
                listY.Add(random.Next(-2, 2));
                listSleepTime.Add(NextDouble(random, 10, 50));
            }



            //长度
            var length = listSleepTime.Count;
            const int maxTotalSleepTime = 5 * 1000;
            if (totalSleepTime > maxTotalSleepTime)
            {
                //统计时间
                totalSleepTime = 0.0D;
                for (var i = 0; i < length; i++)
                {
                    //按比例缩小时间
                    listSleepTime[i] = listSleepTime[i] * (maxTotalSleepTime / totalSleepTime);
                    totalSleepTime += listSleepTime[i];
                }
            }
            //输出总时间
            Console.WriteLine($"滑块滑动总时间:{totalSleepTime}");


            var tracePoints = new TracePoint[length];
            for (var i = 0; i < length; i++)
            {
                tracePoints[i] = new TracePoint
                {
                    XOffset = listX[i],
                    YOffset = listY[i],
                    SleepTime = listSleepTime[i]
                };
            }

            ////输出轨迹值
            //Console.WriteLine("输出轨迹值。");
            //for (var i = 0; i < length; i++)
            //{

            //    Console.WriteLine($"滑块轨迹;X:{tracePoints[i].XOffset},Y:{tracePoints[i].YOffset},Time:{tracePoints[i].SleepTime}");
            //}


            return tracePoints;

        }


        /// <summary>
        /// 返回double类型的随机数
        /// </summary>
        /// <param name="random"></param>
        /// <param name="minDouble"></param>
        /// <param name="maxDouble"></param>
        /// <returns></returns>
        private static double NextDouble(Random random, double minDouble, double maxDouble)
        {
            if (random != null)
            {
                return random.NextDouble() * (maxDouble - minDouble) + minDouble;
            }
            else
            {
                return 0.0D;
            }
        }

    }
}