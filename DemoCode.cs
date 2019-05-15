using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ***.Helpers;
using ***.Models;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Web.Http;
using Newtonsoft.Json;
using ***.Services;
using System.Web.Mvc;

namespace ***.Controllers.Api
{
    public class ***Controller : ApiController
    {
        private ApplicationDbContext _context;
        private readonly OpenTokService _openTokService;


        public ***Controller()
        {

        }

        public ***Controller(ApplicationDbContext context, OpenTokService openTokService)
        {
            _context = context;
            this._openTokService = openTokService;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="param1">Function selector. if 
        /// 1 invite candidate
        /// 2 get results
        /// </param>
        /// <param name="param2">Candidate Informations for invite candidate or getting result informations. splited _P_ every parameter</param>
        /// <returns></returns>
        public dynamic PostGateway(string param1, string param2, HttpRequestMessage request = null)
        {
            dynamic result = null;
            switch (param1)
            {
                case "invite":
                    result = InviteCandidate(param2);
                    break;
                case "webhook":
                    //get results
                    result = ***Webhook(request);
                    break;
                case "getResultAndReport":
                    //get results
                    result = GetResultAndReport();
                    break;
            }

            return result;
        }
        /// <summary>
        /// Invite candidate to *** - *** systems
        /// </summary>
        /// <param name="param2">Candidate Infos</param>
        /// <returns></returns>
        private string InviteCandidate(string param2)
        {
            string[] words = null;
            if (param2 != null && param2 != "")
            {
                words = param2.Split(new string[] { "_P_" }, StringSplitOptions.None);
            }

            var candidateName = words[0];
            var candidateSurname = words[1];
            var candidateEmail = words[2];
            var candidateGender = words[3];
            var invitationCode = words[4];
            var clientId = words[5];
            var apikey = words[6];
            var testId = words[7];
            var languageId = words[8];

            var assessmentId = int.Parse(words[9]);

            var generalUrl = new AssessmentAdditionalSettingsHelper(_context).GetAssessmentAdditionalSetting(assessmentId, "url");

            var token = GetToken(apikey, generalUrl);

            var url = generalUrl + "clients/" + clientId + "/participants";

            var json = new
            {
                firstName = candidateName,
                LastName = candidateSurname,
                gender = candidateGender,
                emailaddress = candidateEmail,
                integrationPartnerParticipantId = invitationCode
            };
            var data = JsonConvert.SerializeObject(json);

            var invitatedCandidateInfos = RestAPICall("POST", url, data, token);

            var participantId = invitatedCandidateInfos["participantId"];
            var participantAssessmentUrl = invitatedCandidateInfos["participantAssessmentUrl"];
            List<JObject> testInfoList = new List<JObject>();
            int i = 0;
            var testIdListArray = testId.Split(',');
            foreach (var testIdStr in testIdListArray)
            {
                dynamic testInfos = new JObject();
                testInfos.testId = testIdStr;
                testInfos.languageId = languageId;
                testInfos.expiryDate = DateTime.Now.AddMonths(1);
                testInfos.order = i;
                testInfoList.Add(testInfos);
                i++;
            }
            var testInfoArray = new JArray(testInfoList);

            var newAssign = new
            {
                tests = testInfoArray,
                expiryDate = DateTime.Now.AddMonths(1)
            };
            var dataAssign = JsonConvert.SerializeObject(newAssign);

            var participantAsignUrl = generalUrl + "clients/" + clientId + "/participants/" + participantId + "/tests";
            var assignCandidate = RestAPICall("POST", participantAsignUrl, dataAssign, token, isInvite: true);

            //var assignCandidateInvitationId = assignCandidate["InvitationId"];
            foreach (var detail in assignCandidate)
            {
                //var invId = detail["InvitationId"];
                ***WebhookResult *** = new ***WebhookResult()
                {
                    InvitationCode = invitationCode,
                    ***ParticipantId = participantId,
                    ***InvitationId = detail["InvitationId"],
                    ***ClientId = clientId,
                    ***AssessmentId = assessmentId,
                    ***TestId = detail["testId"]
                };

                _context.***WebhookResults.Add(***);
            }
           ***WebhookResult ***1 = new ***WebhookResult()
            {
                InvitationCode = invitationCode,
                ***ParticipantId = participantId,
                ***InvitationId = "RoleProfile",
                ***ClientId = clientId,
                ***AssessmentId = assessmentId,
                ***TestId = "RoleProfile",
                ***Event = "testCompleted",
                TimeStamp = DateTime.Now.ToString(),
                WebhookInvokeDate = DateTime.Now
            };

            _context.***WebhookResults.Add(***1);
            _context.SaveChanges();

            return participantAssessmentUrl ?? "";

        }

        /// <summary>
        /// When this assessment completed *** call this API with json parameters
        /// </summary>
        /// <param name="request"> 
        /// {
        ///"event": "testCompleted",
        ///"timeStamp": "2019-01-20T11:07:15.1077954Z",
        ///"participantId": "12-3-c57ffc13-7f62-4eb7-b7c8-***",
        ///"integrationPartnerParticipantId": "partner-id-12342",
        ///"invitationId": "327677cb-a539-4a2f-af1f-***",
        ///"testId": "***"
        ///}
        /// </param>
        /// <returns> status </returns>
        private dynamic ***Webhook(HttpRequestMessage request)
        {
            try
            {
                var content = request.Content;
                string jsonContent = content.ReadAsStringAsync().Result;
                int index1 = jsonContent.IndexOf("{");
                int index2 = jsonContent.IndexOf("}");
                int length = index2 - index1 + 1;
                string tmp = jsonContent.Substring(index1, length);
                var details = JObject.Parse(tmp);

                var ***Event = details["event"] != null ? details["event"].ToString() : "";
                var TimeStamp = details["timeStamp"] != null ? details["timeStamp"].ToString() : "";
                var ***ParticipantId = details["participantId"] != null ? details["participantId"].ToString() : "";
                var ***InvitationId = details["invitationId"] != null ? details["invitationId"].ToString() : "";
                var ***TestId = details["testId"] != null ? details["testId"].ToString() : "";
                var invitationCode = details["integrationPartnerParticipantId"] != null ? details["integrationPartnerParticipantId"].ToString() : "";

                var ***WebhookResult = _context.***WebhookResults.SingleOrDefault(x => x.InvitationCode == invitationCode && x.***TestId == ***TestId && x.***InvitationId == ***InvitationId);

                ***WebhookResult.TimeStamp = TimeStamp;
                ***WebhookResult.***ParticipantId = ***ParticipantId;
                ***WebhookResult.***InvitationId = ***InvitationId;
                ***WebhookResult.***TestId = ***TestId;
                ***WebhookResult.***Event = ***Event;
                ***WebhookResult.WebhookInvokeDate = DateTime.Now;

                _context.Entry(***WebhookResult).State = EntityState.Modified;

                var log = new Log()
                {
                    LogDate = System.DateTime.Now,
                    LogDescription = "***-HookWorked:" + invitationCode,
                    LogType = "Integration"
                };
                _context.Logs.Add(log);
                _context.SaveChanges();

                var ***OtherHooks = _context.***WebhookResults.Where(x => x.InvitationCode == invitationCode && x.***Event == null);
                var invitationCodeGuid = new Guid(invitationCode);
                var inv = _context.Invitations.SingleOrDefault(x => x.InvitationCode == invitationCodeGuid);
                if (***OtherHooks == null || ***OtherHooks.Count() < 1)
                {
                    ApplicationController applicationController = new ApplicationController(_context, _openTokService);
                    applicationController.FinishAssessment(inv.Id, inv.InvitationCode, true, true);
                }
                var result = new
                {
                    status = "webhookWorked"
                };
                return result;
            }
            catch (Exception ex)
            {
                var log = new Log()
                {
                    LogDate = System.DateTime.Now,
                    LogDescription = "***-HookError:" + ex.ToString(),
                    LogType = "Integration"
                };
                _context.Logs.Add(log);
                _context.SaveChanges();

                var result = new
                {
                    status = "error:-"
                };
                return result;
            }
        }

        public dynamic GetResultAndReport(string invitationCode = "", string ***InvitationId = "")
        {
           var isPERSPECTIVES90 = false;

            if (!string.IsNullOrEmpty(invitationCode) && !string.IsNullOrEmpty(***InvitationId))
            {
                var resetResults = _context.***WebhookResults.Where(x => x.InvitationCode == invitationCode);
                foreach(var reset in resetResults)
                {
                    reset.***ResultId = null;
                    _context.Entry(reset).State = EntityState.Modified;
                }
                _context.SaveChanges();
            }
            var results = _context.***WebhookResults.Where(x => x.***ResultId == null && x.WebhookInvokeDate != null);
            if (!string.IsNullOrEmpty(invitationCode))
            {
                isPERSPECTIVES90 = true;
                results = results.Where(x => x.InvitationCode == invitationCode);
            }
            else
            {
                results = results.Where(x => x.***TestId != "PERSPECTIVES90");
            }
            foreach (var result in results.ToList())
            {
                try
                {
                    var assessmentSettings = new AssessmentAdditionalSettingsHelper(_context).GetAssessmentAllAdditionalSetting((int)result.***AssessmentId);
                    var apikey = assessmentSettings.FirstOrDefault(x => x.Key == "apikey").Value;
                    var generalUrl = assessmentSettings.FirstOrDefault(x => x.Key == "url").Value;
                    var testId = assessmentSettings.FirstOrDefault(x => x.Key == "testId").Value;
                    var reportBundleId = assessmentSettings.FirstOrDefault(x => x.Key == "reportBundleId").Value;
                    var normIdStr = assessmentSettings.FirstOrDefault(x => x.Key == "normId").Value;

                    var resultInvitationCode = new Guid(result.InvitationCode);
                    var inv = _context.Invitations.SingleOrDefault(i => i.InvitationCode == resultInvitationCode);

                    if (inv == null)
                    {
                        continue;
                    }

                    var token = GetToken(apikey, generalUrl);
                    var resultList = GetResult(result.***ClientId, result.***ParticipantId, token, (int)result.***AssessmentId, result.InvitationCode, inv.Id, testId, normIdStr,isPERSPECTIVES90, result.***TestId);

                    if(resultList[0] < 1)
                    {
                        continue;
                    }

                    if (resultList[2] < 0)
                    {
                        continue;
                    }

                    result.***ResultId = int.Parse(resultList[0].ToString());
                    _context.Entry(result).State = EntityState.Modified;
                    _context.SaveChanges();

                    var resultIdList = _context.***WebhookResults.Where(x => x.InvitationCode == inv.InvitationCode.ToString()).Select(x => x.***ResultId).ToList();
                    var scoreList = _context.***ResultScores.Include(x => x.***Result).Where(x=>x.***Result.invitationId == inv.Id && x.***Result.***TestId != "PERSPECTIVES90" && x.***Result.***TestId != "RoleProfile" && resultIdList.Contains(x.***ResultId));
                    var candidateFinalScore = 0.0;
                    var candidateScoreCounter = 0;
                    foreach(var score in scoreList)
                    {
                        candidateFinalScore += Double.Parse(score.***tandardisedScore);
                        candidateScoreCounter++;
                    }


                    inv.CandidateScore = candidateFinalScore / (candidateScoreCounter < 1 ? 1 : candidateScoreCounter);
                    _context.Entry(inv).State = EntityState.Modified;

                    var reportIdsList = new List<string>();
                    var normIdsList = new List<string>();

                    var reportIds = assessmentSettings.Where(x => x.Key == "reportId");
                    foreach (var reportId in reportIds)
                    {
                        reportIdsList.Add(reportId.Value);
                    }

                    var normIds = assessmentSettings.Where(x => x.Key == "normId");
                    foreach (var normId in normIds)
                    {
                        normIdsList.Add(normId.Value);
                    }

                    var participantIdsList = new List<string>() { result.***ParticipantId };

                    if (result.***TestId == "PERSPECTIVES90")
                    {
                        var ***ReportUrl = GetReport(generalUrl, token, participantIdsList, result.***ClientId, reportBundleId, reportIdsList, normIdsList);

                        inv.CentralTestHRReportLink = ***ReportUrl;
                    }
                }
                catch (Exception ex)
                {
                    var log1 = new Log()
                    {
                        LogDate = System.DateTime.Now,
                        LogDescription = $"***-Result Error for InvitationCode ({result.InvitationCode}): " + ex.ToString(),
                        LogType = "Integration"
                    };
                    _context.Logs.Add(log1);
                    _context.SaveChanges();
                }

            }
            _context.SaveChanges();
            var log = new Log()
            {
                LogDate = System.DateTime.Now,
                LogDescription = "***-ScheduleWorked",
                LogType = "Integration"
            };
            _context.Logs.Add(log);
            _context.SaveChanges();

            var result1 = new
            {
                success = "true",
                status = "result"
            };
            return result1;

        }

        private List<double> GetResult(string clientId, string participantId, string token, int assessmentId, string invitationCode, int invitationId, string testIdList, string normId, bool isPERSPECTIVES90, string testId)
        {
            var UrlTestIds = "?";
            var testIdListArray = testIdList.Split(',');
            foreach (var testIdStr in testIdListArray)
            {
                if (!isPERSPECTIVES90 && testIdStr.Contains("PERSPECTIVES90"))
                {
                    continue;
                }
                UrlTestIds += "testId=" + testIdStr + "&";
            }

            var generalUrl = new AssessmentAdditionalSettingsHelper(_context).GetAssessmentAdditionalSetting(assessmentId, "url");
            var resultUrl = generalUrl + "clients/" + clientId + "/participants/" + participantId + "/results" + UrlTestIds + "normId=" + normId;
            int ***ResultId = 0;
            double totalScore = 0;
            int scoreCount = 0;
            double standardScore = 0.0;
            var APIresult = RestAPICall("GET", resultUrl, "", token);
            if (invitationCode == APIresult["integrationPartnerParticipantId"].ToString())
            {
                var results = APIresult["results"];
                foreach (var result in results)
                {
                    if (testId == result["testId"].ToString())
                    {
                        ***Result ***Result = new ***Result()
                        {
                            ***TestId = result["testId"],
                            CompletionDate = result["completionDate"],
                            ***ParticipantId = APIresult["participantId"],
                            ***ClientId = APIresult["clientId"],
                            ***IntegrationPartnerId = APIresult["integrationPartnerId"],
                            ***IntegrationPartnerParticipantId = APIresult["integrationPartnerParticipantId"],
                            invitationId = invitationId
                        };
                        _context.***Results.Add(***Result);
                        _context.SaveChanges();

                        ***ResultId = ***Result.id;

                        var scores = result["scores"];
                        foreach (var score in scores)
                        {
                            var eapScore = "";
                            try { eapScore = score["eap"]; } catch (Exception ex) { }
                            ***ResultScore ***ResultScore = new ***ResultScore()
                            {
                                ***ResultId = ***ResultId,
                                ***caleId = ***Result.***TestId + "-" + score["scaleId"],
                                ***PercentileScore = score["percentileScore"],
                                ***RawScore = score["rawScore"],
                                ***tandardisedScore = score["standardisedScore"],
                                ***Eap = eapScore
                            };
                            //if (testId != "PERSPECTIVES90")
                            if (testId.Contains("GCAT"))
                            {
                                totalScore += Double.Parse(***ResultScore.***tandardisedScore);
                                scoreCount++;
                            }
                            _context.***ResultScores.Add(***ResultScore);
                            standardScore = ***ResultScore.***tandardisedScore != null? double.Parse(***ResultScore.***tandardisedScore) : 0.0;
                        }
                        _context.SaveChanges();
                        break;
                    }
                }
            }
            totalScore = totalScore / (scoreCount < 1 ? 1 : scoreCount);
            List<double> scoreList = new List<double>(new double[] { ***ResultId, totalScore, standardScore });
            return scoreList;
        }

        private string GetReport(string generalUrl, string token, List<string> participantIds, string clientId, string reportBundleId, List<string> reportIds, List<string> normIds)
        {
            var url = generalUrl + "clients/" + clientId + "/reports";

            var json = new
            {
                participantIds = participantIds,
                reportBundleId = reportBundleId,
                reportIds = reportIds,
                normIds = normIds
            };
            var data = JsonConvert.SerializeObject(json);

            var reportQueueIdJson = RestAPICall("POST", url, data, token);
            var reportQueueId = reportQueueIdJson["reportQueueId"];

            return reportQueueId;
        }

        public string DownloadReport(string invitationCode, string reportQueueId, string apikey, string clientId, string generalUrl)
        {
            var token = GetToken(apikey, generalUrl);
            try
            {
                var reportBytes = GetReportFrom***(reportQueueId, clientId, generalUrl, token);


                var directory = ConfigurationManager.AppSettings["FileUploadBase"] + "\\" + "\\" +
                                "***Reports\\";

                System.IO.FileStream stream =
                       new FileStream(@"" + directory + invitationCode + ".zip", FileMode.CreateNew);
                System.IO.BinaryWriter writer =
                    new BinaryWriter(stream);
                writer.Write(reportBytes, 0, reportBytes.Length);
                writer.Close();

                var reportUrl = ConfigurationManager.AppSettings["ApplicationRoot"] + "/" +
                        ConfigurationManager.AppSettings["FileUploadVirtualDirectoryAddendum"] +
                        "/***Reports/" + invitationCode + ".zip";
                return reportUrl;
            }
            catch (Exception ex)
            {

                var log = new Log()
                {
                    LogDate = System.DateTime.Now,
                    LogDescription = "***-Report" + ex.ToString(),
                    LogType = "***-Integration"
                };
                _context.Logs.Add(log);
                _context.SaveChanges();

            }
            return "";
        }

        private dynamic GetReportFrom***(string reportQueueId, string clientId, string generalUrl, string token)
        {
            byte[] reportBytes;
            var url = generalUrl + "clients/" + clientId + "/reports/" + reportQueueId;

            var report = RestAPICall("GET", url, "", token, true);

            return report;
        }

        private string GetToken(string apiKey, string generalUrl)
        {
            var url = generalUrl + "token";
            var json = new
            {
                apiKey = apiKey
            };
            var data = JsonConvert.SerializeObject(json);
            var details = RestAPICall("POST", url, data, "");

            return details["token"].ToString();
        }

        public dynamic RestAPICall(string requestMethod, string url, dynamic json, string token, bool isReport = false, bool isInvite = false)
        {
            try
            {
                var httpWebRequest = (HttpWebRequest)WebRequest.Create(url);
                httpWebRequest.Method = requestMethod;

                if (!string.IsNullOrEmpty(token))
                {
                    httpWebRequest.Headers.Add("Authorization", "Bearer " + token);
                }
                if (!string.IsNullOrEmpty(json.ToString()))
                {
                    httpWebRequest.ContentType = "application/json";
                    StreamWriter requestWriter = new StreamWriter(httpWebRequest.GetRequestStream());

                    try
                    {
                        requestWriter.Write(json);
                    }
                    catch
                    {
                        throw;
                    }
                    finally
                    {
                        requestWriter.Close();
                        requestWriter = null;
                    }
                }
                var httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
                if (isReport)
                {
                    MemoryStream ms = new MemoryStream();
                    httpResponse.GetResponseStream().CopyTo(ms);
                    byte[] data = ms.ToArray();
                    return data;
                }

                var result = "";
                using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
                {
                    result = streamReader.ReadToEnd();
                }
                dynamic details = null;
                //dynamic jsonTry = Newtonsoft.Json.JsonConvert.DeserializeObject(result);
                if (isInvite)
                {
                    //var resultNEW = JsonConvert.DeserializeObject<IEnumerable<RootObject>>(result);

                    details = JArray.Parse(result);

                }
                else
                {
                    string jsonContent = result;
                    int index1 = jsonContent.IndexOf("{");
                    int index2 = jsonContent.LastIndexOf("}");
                    int length = index2 - index1 + 1;
                    string tmp = jsonContent.Substring(index1, length);
                    details = JObject.Parse(tmp);
                }
                //dynamic newResult = JsonConvert.DeserializeObject(result);
                return details;
            }
            catch (WebException ex)
            {
                ThrowWithBody(ex);
                return null;
            }
            
        }

        private void ThrowWithBody(WebException wex)
        {
            if (wex.Status == WebExceptionStatus.ProtocolError)
            {
                string responseBody;
                try
                {
                    //Get the message body for rethrow with body included
                    responseBody = new StreamReader(wex.Response.GetResponseStream()).ReadToEnd();

                }
                catch (Exception)
                {
                    //In case of failure to get the body just rethrow the original web exception.
                    throw wex;
                }

                //include the body in the message
                throw new WebException(wex.Message + $" Response body: '{responseBody}'", wex, wex.Status, wex.Response);
            }

            //In case of non-protocol errors no body is available anyway, so just rethrow the original web exception.
            throw wex;
        }
    }
}
