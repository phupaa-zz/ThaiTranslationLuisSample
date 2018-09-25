using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Configuration;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Windows.Forms;




// NOTE: Install the Newtonsoft.Json NuGet package.

namespace TranslatorTextAPI
{
    public partial class Mainform : Form
    {

        static string key;
        static string luisAppId;
        static string subscriptionKey;
        static string luisUri;

        /// </summary>

        private void Write2OutputWindows(string text)
        {
            
            OutputWindows.Text = OutputWindows.Text+text+"\n" + "---------------------- \n";
            OutputWindows.SelectionStart = OutputWindows.Text.Length;
            OutputWindows.ScrollToCaret();
        }

        private void Write2OutputWindows2(string text)
        {
            string MyString;
            string ToShow="";
            string product = "";
            string value = "";
            
            JObject obj = JObject.Parse(text);
            if (obj["query"].ToString() != "")
            {
                Write2OutputWindows(obj["topScoringIntent"]["intent"].ToString());
                MyString = obj["topScoringIntent"]["intent"].ToString();
                if (MyString.StartsWith("C"))  //CreditCard, no entities. 
                {
                    ToShow = "INTENT = " + MyString.Substring(MyString.LastIndexOf(".") + 1) + " on CREDIT CARD.";

                }
                else if (MyString.StartsWith("T"))  //if Topup
                {
                    ToShow = "INTENT = " + MyString.ToString();
                    if (obj["entities"].ToString() != "[]")
                    {
                        var i = 0;

                        // find entity "value" and "product"
                        foreach (var o in obj["entities"])
                        {
                            if (obj["entities"][i]["type"].ToString() == "products")
                            {
                                product = "บัตรทางด่วน";
                            }
                            else if (obj["entities"][i]["type"].ToString() == "builtin.number")
                            {
                                value = obj["entities"][i]["entity"].ToString();
                            };

                            i = i + 1;
                        }
                    }
                    else
                    {
                        value = "not found";
                        product = "not found";
                    }

                    ToShow = ToShow +  ", PRODUCT = " + product + ", VALUE = " + value;
                }

                richTextBox1.AppendText(Environment.NewLine + "[Output] " + ToShow);               
                richTextBox1.SelectionAlignment = HorizontalAlignment.Right;
                richTextBox1.ScrollToCaret();

            }
            else
            {
                richTextBox1.AppendText(Environment.NewLine + "[Output] " + "please click 'send' again'");
                richTextBox1.SelectionAlignment = HorizontalAlignment.Right;
                richTextBox1.ScrollToCaret();
            }
        }


        async void TranslateAndLUIS(string InputText, Boolean debug)
        {
            key = textBox3.Text;
            luisAppId = textBox4.Text;
            subscriptionKey = textBox5.Text;
            luisUri = textBox2.Text;

            string host = "https://api.cognitive.microsofttranslator.com";
            string path = "/translate?api-version=3.0";
            string params_ = "&to=en";  //translate to English
            string uri = host + path + params_;
            DialogResult DiResult;
            // ////////////////// start calling translation service
            string text = textBox1.Text;
            System.Object[] body = new System.Object[] { new { Text = text } };
            var requestBody = JsonConvert.SerializeObject(body);

            if (debug)
            { Write2OutputWindows("requestBody = " + requestBody); }

            var client = new HttpClient();
            var request = new HttpRequestMessage();
            request.Method = HttpMethod.Post;
            request.RequestUri = new Uri(uri);
            request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");
            request.Headers.Add("Ocp-Apim-Subscription-Key", key);

            var response = await client.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();

            var result = JsonConvert.SerializeObject(JsonConvert.DeserializeObject(responseBody), Formatting.Indented);

            if (debug) { Write2OutputWindows("result = " + result); }

            // I noticed that SerializeObject put "result" in [], so I remove it.  Otherwise, JObject.Parse(result) return error.
            // as of 2018-05-29.

            result = result.Remove(result.Length - 1);
            result = result.Substring(1);
            try
            {
                JObject obj = JObject.Parse(result);
                if (debug) { Write2OutputWindows("obj-translations-0-text = " + obj["translations"][0]["text"].ToString()); }            
                // ////////////////// start calling LUIS service
                var LUISclient = new HttpClient();
                var queryString = HttpUtility.ParseQueryString(string.Empty);
                // The request header contains your subscription key
                LUISclient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", subscriptionKey);
                // The "q" parameter contains the utterance to send to LUIS
                queryString["q"] = obj["translations"][0]["text"].ToString(); ; // this is an output from Translation API. 

                var LuisUri = luisUri + luisAppId + "?" + queryString;
                var LUISresponse = await LUISclient.GetAsync(LuisUri);

                string strResponseContent = await LUISresponse.Content.ReadAsStringAsync();
                if (debug) { Write2OutputWindows("strResponseContent = " + strResponseContent); }

                Write2OutputWindows2(strResponseContent);
            }
            catch
            {
            
                DiResult = MessageBox.Show("Calling Azure Cognitive service API Failed, check your keys and settings. ");
            }
        }
        
        public Mainform()
        {
            InitializeComponent();
            textBox3.Text = ReadSetting("translationapi_subscription_key");
            textBox4.Text= ReadSetting("application_id");
            textBox5.Text = ReadSetting("luis_subscription_key");
            textBox2.Text= ReadSetting("luis_uri");
            key = textBox3.Text;
            luisAppId = textBox4.Text;
            subscriptionKey = textBox5.Text;
            luisUri = textBox2.Text;
        }

        
        string ReadSetting(string key)
        {
            try
            {
                var appSettings = ConfigurationManager.AppSettings;
                string result = appSettings[key] ?? "Not Found";
                Write2OutputWindows("Read " + key + ":" + result);
                return result;
            }
            catch (ConfigurationErrorsException)
            {
                Write2OutputWindows("Error reading app settings");
                return "Error reading app settings";
            }
        }

        void AddUpdateAppSettings(string key, string value)
        {
            try
            {
                var configFile = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                var settings = configFile.AppSettings.Settings;
                if (settings[key] == null)
                {
                    settings.Add(key, value);
                }
                else
                {
                    settings[key].Value = value;
                }
                configFile.Save(ConfigurationSaveMode.Modified);
                ConfigurationManager.RefreshSection(configFile.AppSettings.SectionInformation.Name);
                Write2OutputWindows("Write " + key + ":" + value);
            }
            catch (ConfigurationErrorsException)            {
                
                Write2OutputWindows("Error writing app settings");
            }
        }


        private void button2_Click(object sender, EventArgs e)
        {

            //GetLanguages();  //showing a list of support languages.
            richTextBox1.AppendText(Environment.NewLine  + Environment.NewLine + "[Input] " + textBox1.Text);
            richTextBox1.SelectionAlignment = HorizontalAlignment.Left;
            richTextBox1.ScrollToCaret();
            TranslateAndLUIS(textBox1.Text, true);            

        }

 
        private void textBox1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                richTextBox1.AppendText(Environment.NewLine + Environment.NewLine + "[Input] " + textBox1.Text);
                richTextBox1.SelectionAlignment = HorizontalAlignment.Left;
                richTextBox1.ScrollToCaret();
                TranslateAndLUIS(textBox1.Text, true);
            }
        }


        /// Code Backup///////////////////////////////////////////////////////////////////////////////////////

        async void GetLanguages()
        {
            //reference: https://docs.microsoft.com/en-us/azure/cognitive-services/translator/quickstarts/csharp#translate-text 

            string host = "https://api.cognitive.microsofttranslator.com";
            string path = "/languages?api-version=3.0";

            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", key);
            var uri = host + path + "&Accept-Language=th";

            Write2OutputWindows(uri);

            var response = await client.GetAsync(uri);
            var result = await response.Content.ReadAsStringAsync();
            var json = JsonConvert.SerializeObject(JsonConvert.DeserializeObject(result), Formatting.Indented);
            // Note: If writing to the console, set this.
            // Console.OutputEncoding = UnicodeEncoding.UTF8;
            // System.IO.File.WriteAllBytes(@"c:\temp\output.txt", Encoding.UTF8.GetBytes(json));
            System.IO.File.AppendAllText(@"c:\temp\output.txt", json);
            Write2OutputWindows(json);

        }

        async void Translate(string InputText, Boolean debug)
        {
            //reference: https://docs.microsoft.com/en-us/azure/cognitive-services/translator/quickstarts/csharp#translate-text 

            key = textBox3.Text;
            luisAppId = textBox4.Text;
            subscriptionKey = textBox5.Text;
            luisUri = textBox2.Text;

            string host = "https://api.cognitive.microsofttranslator.com";
            string path = "/translate?api-version=3.0";
            // Translate to German and Italian.
            //string params_ = "&to=de&to=it&to=en";
            string params_ = "&to=en";
            string uri = host + path + params_;
            
            //string text = "ผมเหลือเงินอยู่ในบัญชีเท่าไร";
            string text = textBox1.Text;
            System.Object[] body = new System.Object[] { new { Text = text } };
            var requestBody = JsonConvert.SerializeObject(body);

            if (debug)
            { Write2OutputWindows("requestBody = " + requestBody); }


            using (var client = new HttpClient())
            using (var request = new HttpRequestMessage())
            {
                request.Method = HttpMethod.Post;
                request.RequestUri = new Uri(uri);
                request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");
                request.Headers.Add("Ocp-Apim-Subscription-Key", key);


                var response = await client.SendAsync(request);
                var responseBody = await response.Content.ReadAsStringAsync();

                
                var result = JsonConvert.SerializeObject(JsonConvert.DeserializeObject(responseBody), Formatting.Indented);



                if (debug) { Write2OutputWindows("result = " + result); }

                result = result.Remove(result.Length - 1);
                result = result.Substring(1);
                JObject obj = JObject.Parse(result);

                if (debug) { Write2OutputWindows("obj-translations-0-text = " + obj["translations"][0]["text"].ToString()); }

                Write2OutputWindows(obj["translations"][0]["text"].ToString());

            }
        }
        async void CallLUIS(string input, Boolean debug)
        {
            key = textBox3.Text;
            luisAppId = textBox4.Text;
            subscriptionKey = textBox5.Text;
            luisUri = textBox2.Text;
            //concept: https://docs.microsoft.com/en-us/azure/cognitive-services/LUIS/luis-concept-entity-types 
            var client = new HttpClient();
            var queryString = HttpUtility.ParseQueryString(string.Empty);

            // The request header contains your subscription key
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", subscriptionKey);
            // The "q" parameter contains the utterance to send to LUIS
            queryString["q"] = "";
            var uri = luisUri + luisAppId + "?" + queryString;
            var response = await client.GetAsync(uri);

            string strResponseContent = await response.Content.ReadAsStringAsync();
            if (debug) { Write2OutputWindows("strResponseContent = " + strResponseContent); }

            Write2OutputWindows2(strResponseContent);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            AddUpdateAppSettings("translationapi_subscription_key", textBox3.Text);
            AddUpdateAppSettings("application_id", textBox4.Text);
            AddUpdateAppSettings("luis_subscription_key", textBox5.Text);
            AddUpdateAppSettings("luis_uri", textBox2.Text);
        }

        private void label5_Click(object sender, EventArgs e)
        {

        }
    }
}
