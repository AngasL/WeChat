using System;
using System.Net;
using System.Text;
using System.Web.Mvc;
using System.Xml;
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

            var responseResult = GetDefaultMessageContent();
            string msgType = "text";
            if (messageType != null && messageType.Value == "text")
            {
                var originalRequestContent = rootElement.Element("Content").Value.Trim();
                responseResult = GetResponseContent(originalRequestContent, out msgType);
            }

            responseResult = FormatResult(rootElement, responseResult, msgType);

            return Content(responseResult, "text/xml", Encoding.UTF8);
        }

        private static string GetDefaultMessageContent()
        {
            // todo: add dictionary functionality.
            var resultMessage = "请输入以下信息格式：\r\n"
                           + "1.天气 城市名称\r\n"
                           + "例如：天气北京（天气+城市名字）\r\n"
                           + "2.翻译 英汉互译 \r\n"
                           + "例如： 翻译 apple 或者 翻译 苹果（翻译+翻译内容） \r\n"
                           + "3.精选图文\r\n"
                           + "例如：微信精选\r\n"
                           + "4.用户自定义\r\n"
                           + "你想使用什么功能，请给月光留言。\r\n"
                           + "微信号: yueguang112358";

            return resultMessage;
        }

        private string GetResponseContent(string originalRequestContent, out string msgType)
        {
            msgType = "text";
            var responseContent = GetDefaultMessageContent();

            if (originalRequestContent.Contains(Weather))
            {
                var cityName = originalRequestContent.Substring(2).Trim();

                responseContent = GetWeatherData(cityName);
            }
            if (originalRequestContent.Contains(Translate))
            {
                var translateCandidateContent = originalRequestContent.Substring(2).Trim();

                responseContent = GetTranslatedData(translateCandidateContent);
            }
            if (originalRequestContent.Contains(Weixin) || originalRequestContent.Contains(Choiceness))
            {
                msgType = "news";
                responseContent = GetNewsData();
            }
            else if (originalRequestContent.StartsWith(JokePrefix))
            {
                var jokeName = originalRequestContent.Split(' ')[1];
                responseContent = GetJokeData(jokeName);
            }

            return responseContent;
        }

        private string FormatResult(XElement rootElement, string responseContent, string msgType)
        {
            var suffix = "<Content><![CDATA[{1}]]></Content>" + "</xml>";

            if (msgType == "news")
            {
                suffix = "<ArticleCount>6</ArticleCount>"
              + "<Articles>"
              + "{1}"
              + "</Articles>"
              + "</xml>";
            }

            return string.Format(@"<xml>"
              + "<ToUserName>" + rootElement.Element("FromUserName").Value + "</ToUserName>"
              + "<FromUserName>" + rootElement.Element("ToUserName").Value + "</FromUserName>"
              + "<CreateTime>" + rootElement.Element("CreateTime").Value + "</CreateTime>"
              + "<MsgType><![CDATA[{0}]]></MsgType>"
              + suffix, msgType, responseContent);
        }

        private string GetTranslatedData(string translateCandidateContent)
        {
            var targetUrl = string.Format(@"http://fanyi.youdao.com/openapi.do?keyfrom=Angas112358&key=481379024&type=data&doctype=json&version=1.1&q={0}", translateCandidateContent);
            var downloadString = GetDownloadString(targetUrl);

            var translation = JsonConvert.DeserializeObject<JObject>(downloadString)
                .Properties()
                .Where(p => p.Name == "translation")
                .FirstOrDefault()
                .Value.FirstOrDefault().ToString();

            return translation;
        }

        private string GetNewsData()
        {
            var targetUrl = "http://v.juhe.cn/weixin/query?pno=1&ps=6&dtype=xml&key=9229f60e403167df4a9826e7d36e6d79";
            var downloadString = GetDownloadString(targetUrl);

            var xDocument = JsonConvert.DeserializeXNode(downloadString, "root");
            var articleitems = xDocument
                .Root
                .Element("result")
                .Elements("list")
                .Select(item => BuildArticleItems(item));

            var articleContent = string.Empty;

            foreach (var item in articleitems)
            {
                articleContent += item.ToString();
            }

            return articleContent;
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

        private string GetWeatherData(string city)
        {
            var targetUrl = "http://v.juhe.cn/weather/index?dtype=xml&format=1&key=df13c9ded7d616ff9432ed1955e57fa3&cityname=" + city;
            string downloadString = GetDownloadString(targetUrl);

            var xml = XDocument.Parse(downloadString);
            if (xml.Root.Element("error_code").Value != "0")
            {
                return "请输入有效城市名称.\r\n"
                    + "例如：北京\r\n";
            }

            var resultXml = xml.Root.Element("result");
            var skyXml = resultXml.Element("sk");

            var todayXml = resultXml.Element("today");
            var weatherContent = todayXml.Element("city").Value + "今天：\r\n"
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

                weatherContent += currentDate.AddDays(i).ToString("yyyy-MM-dd") + " :\r\n"
                 + "温度范围： " + element.Element("temperature").Value + "\r\n"
                 + "天气： " + element.Element("weather").Value + "\r\n"
                 + "风力： " + element.Element("wind").Value + "\r\n";

                i++;
            }

            return weatherContent;
        }

        private static string GetDownloadString(string targetUrl)
        {
            var webClient = new WebClient();
            webClient.Headers.Add("Content-Type", "application/x-www-form-urlencoded; charset=UTF-8");
            var downloadString = webClient.DownloadString(targetUrl);

            return downloadString;
        }
    }
}
