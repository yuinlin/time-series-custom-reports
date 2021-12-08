using System;
using System.Data;
using System.Reflection;
using ReportPluginFramework;
using ReportPluginFramework.ReportData;
using ReportPluginFramework.ReportData.TimeSeriesComputedStatistics;
using ReportPluginFramework.ReportData.TimeSeriesData;
using ReportPluginFramework.ReportData.TimeSeriesDescription;
using System.Collections.Generic;
using System.Net.Http;
using Newtonsoft.Json;

namespace ContinuousDataProductionNamespace
{
    public class ReportSpecificFunctions
    {
        private static ServiceStack.Logging.ILog Log = ServiceStack.Logging.LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public static int GetNumberOfHistoricalPointsAvailable(string urlPrefix, string locationIdentifier, int httpRepeatCallLimit, int httpCallTimeoutInSeconds)
        {
            string path = urlPrefix;
            string answer = "";

            try
            {
                path += System.Net.WebUtility.UrlEncode(locationIdentifier);
                path += "&startindex=0&limit=1";

                answer = CallHttpClient(path, httpRepeatCallLimit, httpCallTimeoutInSeconds);

                if (!string.IsNullOrEmpty(answer))
                {
                    dynamic dynData = JsonConvert.DeserializeObject(answer);
                    int numberOfPoints = dynData.numberMatched;
                    Log.InfoFormat("GetNumberOfHistoricalPointsAvailable: Found '{0}' historical points for location '{1}' from Url '{2}'", numberOfPoints, locationIdentifier, path);
                    return numberOfPoints;
                }
            }
            catch (Exception ex)
            {
                Log.Error("Exception in call to GetNumberOfHistoricalPointsAvailable " + Environment.NewLine, ex);
                Log.InfoFormat("Fail to retrieve number of Historical points from Url '{0}', http call returned '{1}', so return number of Historical = -1", path, answer);
            }

            return -1;
        }

        public static string CallHttpClient(string path, int httpRepeatCallLimit, int httpCallTimeoutInSeconds)
        {
            int callNumber = 1;

            while (callNumber <= httpRepeatCallLimit)
            {
                try
                {
                    return CallHttpClient(path, httpCallTimeoutInSeconds);
                }
                catch (Exception ex)
                {
                    Log.Error("CallHttpClient number " + callNumber + ": exception while fetching data from url path = " + Environment.NewLine + path, ex);
                    Log.Error("Call " + callNumber + " of " + httpRepeatCallLimit + " with timeout " + httpCallTimeoutInSeconds + " failed to fetch data!");
                    callNumber++;
                }
            }
            return "";
        }

        public static string CallHttpClient(string path, double timeoutInSeconds)
        {
            using (HttpClient client = new HttpClient())
            {
                Log.Debug("GetHttpData - begin by setting client timeout");
                client.Timeout = TimeSpan.FromSeconds(timeoutInSeconds);
                Log.Debug("GetHttpData - after set client timeout to " + timeoutInSeconds.ToString() + " seconds");

                HttpResponseMessage response = client.GetAsync(path).Result;

                Log.DebugFormat("GetHttpData after call 'client.GetAsync(path).Result', response status is {0}", response.StatusCode);

                if (!response.IsSuccessStatusCode) return "";
                Log.Debug("GetHttpData response is success");

                string data = response.Content.ReadAsStringAsync().Result;
                Log.Debug("GetHttpData after call 'response.Content.ReadAsStringAsync().Result'");
                return data;
            }
        }
    }
}
