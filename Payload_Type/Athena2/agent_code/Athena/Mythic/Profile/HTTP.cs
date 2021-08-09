using Newtonsoft.Json;
using System.Net.Http;
using System.Threading.Tasks;

namespace Athena.Mythic.Profile
{
    //I wonder if I can expand upon this class to call the same function, but change the functions called base on the comms channel chosen
    public class HTTP
    {
        private async Task<string> SendPOSTAsync(string url, object obj)
        {
            string json = JsonConvert.SerializeObject(obj);

            var content = new StringContent(json);
            var response = await Globals.client.PostAsync(url, content);
            return await response.Content.ReadAsStringAsync();
        }
        private string SendPOST(string url, object obj)
        {
            string json = JsonConvert.SerializeObject(obj);

            var content = new StringContent(json);
            var response = Globals.client.PostAsync(url, content);
            return response.Result.Content.ReadAsStringAsync().Result;
        }

        private async Task<string> SendGETAsync(string url)
        {
            var response = await Globals.client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            return await response.Content.ReadAsStringAsync();
        }
        private string SendGET(string url)
        {
            var response = Globals.client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            return response.Result.Content.ReadAsStringAsync().Result;
        }
    }
}
