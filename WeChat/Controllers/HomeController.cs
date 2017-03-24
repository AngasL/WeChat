using System;
using System.Net;
using System.Text;
using System.Web.Mvc;
using System.Xml.Linq;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace WeChat.Controllers
{
    public class HomeController : Controller
    {
        private const string Weather = "天气";
        private const string JokePrefix = "笑话";
        private const string Translate = "翻译";
        private const string Weixin = "微信";
        private const string Choiceness = "精选";

        [HttpGet]
        [ActionName("Index")]
        public ActionResult Get(string signature, string timestamp, string nonce, string echostr)
        {
            return Content(echostr); //返回随机字符串则表示验证通过
        }

        [HttpPost]
        [ActionName("Index")]
        public ActionResult Post()
        {
            var stream = this.Request.InputStream;
            var bytes = new byte[stream.Length];
            stream.Read(bytes, 0, (int)stream.Length);
            var RequestResult = Encoding.UTF8.GetString(bytes);
            var xmlRequest = XDocument.Parse(RequestResult);
            var rootElement = xmlRequest.Root;
            var messageType = rootElement.Element("MsgType");

            var responseResult = GetDefaultMessageResponseResult(rootElement);

            if (messageType != null && messageType.Value == "text")
            {
                responseResult = GetResponseResult(rootElement);
            }

            return Content(responseResult, "text/xml", Encoding.UTF8);
        }

        private static string GetDefaultMessageContent()
        {
            // todo: add dictionary functionality.
            var resultMessage = "请输入以下信息格式：\r\n"
                           + "1.天气 城市名称\r\n"
                           + "例如：天气北京（天气+城市名字）\r\n"
                           + "2.翻译 英汉互译 \r\n"
                           + "例如： 翻译 apple 或者 翻译 苹果（翻译+翻译内容）"
                           + "3.精选图文\r\n"
                           + "例如：微信精选\r\n"
                           + "4.用户自定义\r\n"
                           + "你想使用什么功能，请给月光留言。\r\n"
                           + "微信号: yueguang112358";

            //var celeberateOneWeek = DateTime.Parse("2017-03-29");
            //var isInCeleberateRange = celeberateOneWeek.AddDays(-7) <= DateTime.UtcNow;

            //if (!isInCeleberateRange)
            //{
            //    resultMessage = "Congratulation on Chinese Male football beat South Korea.";
            //}


            return resultMessage;
        }

        private static string GetDefaultMessageResponseResult(XElement rootElement)
        {
            return "<xml>"
               + "<ToUserName>" + rootElement.Element("FromUserName").Value + "</ToUserName>"
               + "<FromUserName>" + rootElement.Element("ToUserName").Value + "</FromUserName>"
               + "<CreateTime>" + rootElement.Element("CreateTime").Value + "</CreateTime>"
               + "<MsgType><![CDATA[text]]></MsgType>"
               + "<Content><![CDATA[" + GetDefaultMessageContent() + "]]></Content>"
               + "</xml>";
        }

        private string GetResponseResult(XElement rootElement)
        {
            var responseResult = GetDefaultMessageResponseResult(rootElement);

            var pureContent = rootElement.Element("Content").Value.Trim();

            if (pureContent.Contains(Weather))
            {
                var cityName = pureContent.Substring(2).Trim();

                responseResult = GetWeatherData(rootElement, cityName);
            }
            if (pureContent.Contains(Translate))
            {
                var translateCandidateContent = pureContent.Substring(2).Trim();

                responseResult = GetTranslatedData(rootElement, translateCandidateContent);
            }
            if (pureContent.Contains(Weixin) || pureContent.Contains(Choiceness))
            {
                responseResult = GetNewsData(rootElement);
            }
            else if (pureContent.StartsWith(JokePrefix))
            {
                var jokeName = pureContent.Split(' ')[1];
                responseResult = GetJokeData(jokeName);
            }

            return responseResult;
        }

        private string GetTranslatedData(XElement rootElement, string translateCandidateContent)
        {
            var webClient = new WebClient();
            var url = string.Format(@"http://fanyi.youdao.com/openapi.do?keyfrom=Angas112358&key=481379024&type=data&doctype=json&version=1.1&q={0}", translateCandidateContent);
            webClient.Headers.Add("Content-Type", "application/x-www-form-urlencoded; charset=UTF-8");
            var downloadString = webClient.DownloadString(url);

            var translation = JsonConvert.DeserializeObject<JObject>(downloadString)
                .Properties()
                .Where(p => p.Name == "translation")
                .FirstOrDefault()
                .Value.FirstOrDefault().ToString();

            return "<xml>"
              + "<ToUserName>" + rootElement.Element("FromUserName").Value + "</ToUserName>"
              + "<FromUserName>" + rootElement.Element("ToUserName").Value + "</FromUserName>"
              + "<CreateTime>" + rootElement.Element("CreateTime").Value + "</CreateTime>"
              + "<MsgType><![CDATA[text]]></MsgType>"
              + "<Content><![CDATA[" + translation + "]]></Content>"
              + "</xml>";
        }

        private string GetNewsData(XElement rootElement)
        {
            var webClient = new WebClient();

            var url = "http://v.juhe.cn/weixin/query?pno=1&ps=6&dtype=xml&key=9229f60e403167df4a9826e7d36e6d79";
            webClient.Headers.Add("Content-Type", "application/x-www-form-urlencoded; charset=UTF-8");
            var downloadString = webClient.DownloadString(url);
            var articleitems = XDocument.Parse(downloadString).Root.Element("result").Element("list").Elements().Select(item => BuildArticleItems(item));

            var articleContent = string.Empty;
            foreach (var item in articleitems)
            {
                articleContent += item.ToString();
            }

            return "<xml>"
              + "<ToUserName>" + rootElement.Element("FromUserName").Value + "</ToUserName>"
              + "<FromUserName>" + rootElement.Element("ToUserName").Value + "</FromUserName>"
              + "<CreateTime>" + rootElement.Element("CreateTime").Value + "</CreateTime>"
              + "<MsgType><![CDATA[news]]></MsgType>"
              + "<ArticleCount>6</ArticleCount>"
              + "<Articles>"
              + articleContent
              + "</Articles>"
              + "</xml>";
        }

        private XElement BuildArticleItems(XElement item)
        {
            return new XElement("item",
                new XElement("Title", item.Element("title").Value),
                new XElement("Description", item.Element("title").Value),
                new XElement("PicUrl", item.Element("firstImg").Value),
                new XElement("Url", item.Element("url").Value));
        }

        private string GetJokeData(string jokeName)
        {
            throw new NotImplementedException();
        }

        private string GetWeatherData(XElement rootElement, string city)
        {
            var webClient = new WebClient();

            var url = "http://v.juhe.cn/weather/index?dtype=xml&format=1&key=df13c9ded7d616ff9432ed1955e57fa3&cityname=" + city;
            webClient.Headers.Add("Content-Type", "application/x-www-form-urlencoded; charset=UTF-8");

            var downloadString = webClient.DownloadString(url);

            var xml = XDocument.Parse(downloadString);
            if (xml.Root.Element("error_code").Value != "0")
            {
                return "请输入有效城市名称.\r\n"
                    + "例如：北京\r\n";
            }

            var resultXml = xml.Root.Element("result");
            var skyXml = resultXml.Element("sk");

            var todayXml = resultXml.Element("today");
            var weatherString = todayXml.Element("city").Value + "今天：\r\n"
                + "当前温度： " + skyXml.Element("temp").Value + "\r\n"
                + "风向： " + skyXml.Element("wind_direction").Value + "\r\n"
                + "风力： " + skyXml.Element("wind_strength").Value + "\r\n"
                + "空气湿度： " + skyXml.Element("humidity").Value + "\r\n"
                + "温度范围： " + todayXml.Element("temperature").Value + "\r\n"
                + "天气： " + todayXml.Element("weather").Value + "\r\n"
                + "预报时间： " + skyXml.Element("time").Value + "\r\n";

            var futureXml = resultXml.Element("future");

            var i = 0;
            var currentDate = DateTime.Now;

            foreach (var element in futureXml.Elements())
            {
                if (i == 0)
                {
                    i++;
                    continue;
                }

                weatherString += currentDate.AddDays(i).ToString("yyyy-MM-dd") + " :\r\n"
                 + "温度范围： " + element.Element("temperature").Value + "\r\n"
                 + "天气： " + element.Element("weather").Value + "\r\n"
                 + "风力： " + element.Element("wind").Value + "\r\n";

                i++;
            }

            return "<xml>"
              + "<ToUserName>" + rootElement.Element("FromUserName").Value + "</ToUserName>"
              + "<FromUserName>" + rootElement.Element("ToUserName").Value + "</FromUserName>"
              + "<CreateTime>" + rootElement.Element("CreateTime").Value + "</CreateTime>"
              + "<MsgType><![CDATA[text]]></MsgType>"
              + "<Content><![CDATA[" + weatherString + "]]></Content>"
              + "</xml>";
        }
    }
}
