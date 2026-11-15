using SideWidget.Core;
static class WebTranslationChecks
{
 public static int Run()
 {
  var count=0;
  void Check(bool value,string label){if(!value)throw new Exception("Web translation: "+label);count++;}
  void Reject(Action action){try{action();}catch(CommandFault){count++;return;}throw new Exception("Invalid web translation accepted");}
  var request=WebTranslationRequest.Create("google","Hello & #мир?","auto","ru");
  Check(request.Uri.Host=="translate.google.com"&&request.Uri.Query.Contains("sl=auto")&&request.Uri.Query.Contains("tl=ru"),"Google language URL");
  Check(request.Uri.Query.Contains("Hello%20%26%20%23"),"encoded input");
  Check(WebTranslationRequest.Create("yandex","Hello","en","ru").Uri.Host=="translate.yandex.ru","Yandex fixed host");
  Reject(()=>WebTranslationRequest.Create("google",new string('x',5001),"en","ru"));
  Check(WebTranslationRequest.Create("yandex",new string('字',10000),"en","ru").Text.Length==10000,"Yandex text limit");
  Reject(()=>WebTranslationRequest.Create("google"," ","en","ru"));
  Reject(()=>WebTranslationRequest.Create("other","Hello","en","ru"));
  Reject(()=>WebTranslationRequest.Create("google","Hello","en&text=evil","ru"));
  Reject(()=>WebTranslationRequest.Create("google","Hello","en","auto"));
  Check(request.Allows("https://translate.google.com/?text=Hello"),"exact HTTPS host");
  foreach(var url in new[]{"https://translate.google.com.evil.test/","http://translate.google.com/","https://x@translate.google.com/","https://translate.google.com:8443/","https://sidewidget.local/","https://consent.google.com/","https://accounts.google.com/","file:///c:/","javascript:alert(1)"})Check(!request.Allows(url),"reject other origins");
  Check(request.ReadResult("{\"state\":\"ready\",\"text\":\"Привет <b>мир</b>\"}")=="Привет <b>мир</b>","plain text response");
  Check(request.ReadResult("{\"state\":\"waiting\"}")==null,"pending response");
  Check(request.AllowsNavigation("https://www.google.com/sorry/index?continue=test"),"Google manual challenge allowed");
  Check(!request.Allows("https://www.google.com/sorry/index"),"challenge origin never read for translation");
  Check(request.RequiresVerification("https://translate.google.com/sorry/index"),"same-origin Google challenge");
  var yandex=WebTranslationRequest.Create("yandex","Hello","en","ru");
  Check(!yandex.RequiresVerification("https://translate.yandex.ru/showcaptchafast?retpath=test"),"automatic Yandex verification stays hidden");
  Check(yandex.AllowsNavigation("https://translate.yandex.ru/showcaptchafast?retpath=test"),"automatic Yandex verification may finish normally");
  Check(yandex.RequiresVerification("https://translate.yandex.ru/showcaptcha?retpath=test"),"manual Yandex challenge shown");
  Check(yandex.AllowsNavigation("https://translate.yandex.ru/showcaptcha"),"Yandex manual challenge allowed");
  Check(!yandex.RequiresVerification("https://translate.yandex.ru/?text=showcaptcha"),"query text is not a challenge");
  foreach(var url in new[]{"https://www.google.com/search", "https://www.google.com/sorryevil", "https://www.google.com.evil.test/sorry/index", "http://www.google.com/sorry/index", "https://x@www.google.com/sorry/index", "https://www.google.com:8443/sorry/index", "https://accounts.google.com/"})Check(!request.AllowsNavigation(url),"challenge keeps strict boundary");
  Reject(()=>request.ReadResult("{\"state\":\"blocked\"}"));
  Reject(()=>request.ReadResult("{\"state\":\"ready\",\"text\":\"\"}"));
  Reject(()=>request.ReadResult("null"));
  return count;
 }
}
