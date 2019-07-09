﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Net;
using AskForm.Application;
using AskForm.CenterApplication;
namespace Msd_V2
{
    public class NewAction
    {
        public static void Exec()
        {
            var list = GetList();

            list.ForEach(x =>
            {
                _exec(x);
                //Console.WriteLine($"社招执行了ID为{x}的数据");
            });
        }

        private static List<long> GetList()
        {
            var statetime = DateTime.Now.AddDays(-1).ToString("yyyyMMddhhmmss");
            var endtime = DateTime.Now.AddDays(1).ToString("yyyyMMddhhmmss");
            string Url = $@"http://api.beisenapp.com/RecruitV2/{NewSetting.Tenant_ID}/applicant/GetApplicantIdsByDateAndStatus?start_time={statetime}&end_time={endtime}&phase_id=S02&status_id=U03";

            var result = HttpHelper.HttpGet(Url);
            List<long> ids = JsonConvert.DeserializeObject<List<long>>(result);
            ids.Reverse();
            return ids;
        }


        public static void _exec(long id)
        {
            string Url = $@"http://api.beisenapp.com/RecruitV2/{NewSetting.Tenant_ID}/Applicant/ById/{id}?language={1}&photo_base64={0}&has_Long={1}";
            var Result = HttpHelper.HttpGet(Url);
            if (string.IsNullOrEmpty(Result) || Result == "[]") { return; }

            List<Applicant> AppList = JsonConvert.DeserializeObject<List<Applicant>>(Result);
            var app = AppList[0];
            var ItemObj = app.Profile.Items.Find(x => x.PropertyName == "Name");
            if (ItemObj == null)
            {
                //ErrorLog.WriteLog(new Msd_V2.ErrorObj()
                //{
                //    ErrorMsg ="用户名为空",
                //    ID = id,
                //    UserName =""
                //});
                return;
            }


            //获取用户名和邮箱
            var LastName = ItemObj.Value.Substring(0, 1);
            var FirstName = ItemObj.Value.Substring(1);
            var EmailObj = app.Profile.Items.Find(x => x.PropertyName == "email");
            if (EmailObj == null)
            {
                //ErrorLog.WriteLog(new Msd_V2.ErrorObj()
                //{
                //    ErrorMsg ="未填写邮箱",
                //    ID = id,
                //    UserName = ItemObj.Value
                //});
                Console.WriteLine(ItemObj.Value + "未填写邮箱");
                return;
            }

            var Email = EmailObj.Value;
            //if (Email.IndexOf("@qq.com") > 0 || Email.IndexOf("@163.com") > 0) {
            //    return;
            //}

            //判断邮箱是否已发送过五次以上
            var emailSendCount = SelectCountByEmail(Email);
            if (emailSendCount > 5)
            {
                //ErrorLog.WriteLog(new Msd_V2.ErrorObj()
                //{
                //    ErrorMsg ="已连续发送超过5次,可能是标记不成功,已跳过",
                //    ID = id,
                //    UserName = ItemObj.Value
                //}, "RepeatEmail.txt");
                return;
            }

            //重复发送测评
            //if (Result.IndexOf("extshfxychffscp_102854_2003095649") > 0)
            //{
            //    UpdateEmailToSending(Email);
            //    return;
            //}

            //Email = "442485840@qq.com";

            //获取JobID(根据JOBID获取岗位序列)
            string jobid = app.ApplyJobSummaries[app.ApplyJobSummaries.Count - 1].JobCustomFields.Find(x => x.PropertyName == "extJobSequence_102854_300134805")?.Value;
            jobid = GetJobID(jobid);

            if (string.IsNullOrEmpty(jobid) || jobid == "0")
            {
                //ErrorLog.WriteLog(new Msd_V2.ErrorObj()
                //{
                //    ErrorMsg = "JobID为空",
                //    ID = id,
                //    UserName = ItemObj.Value
                //});
                return;
            }

            //根据该ID 在测评做完之后 更新测评状态
            string jobid2 = app.ApplyJobSummaries[app.ApplyJobSummaries.Count - 1].JobId;
            //获取NormsetId
            string NormsetId = app.ApplyJobSummaries[app.ApplyJobSummaries.Count - 1].JobCustomFields.Find(x => x.PropertyName == "extszcpcmlx_102854_1709877407")?.Code;

            if (string.IsNullOrEmpty(NormsetId))
            {
                //ErrorLog.WriteLog(new Msd_V2.ErrorObj()
                //{
                //    ErrorMsg = "NormsetId为空",
                //    ID = id,
                //    UserName = ItemObj.Value
                //});
                return;
            }

            NormsetId = string.IsNullOrEmpty(NormsetId) ? "1100" : NormsetId;

            NormsetId = NorID.Nors.ContainsKey(NormsetId) ? NorID.Nors[NormsetId] : "1060";

            //app.ApplyJobSummaries.ForEach(x =>
            //{
            //    SignJobToU15(app.ApplicantId, x.JobId);
            //    jobid2 += string.IsNullOrEmpty(jobid2) ? x.JobId:string.Empty;
            //    jobid += string.IsNullOrEmpty(jobid) ? x.JobCustomFields[0].Code : string.Empty;
            //});

            long EntryID = 0;
            //如果已经投递过岗位并且已完成测评 则按新职位(需要更新该CadidateId的JobID)模拟请求Callback
            if (IsTested(app.CadidateId, out EntryID))
            {
                //更新该用户的岗位编号
                UpdateJobID(EntryID, Convert.ToInt64(jobid));
                //模拟请求
                SimulationExec(app.CadidateId);
                return;
            }


            ServicePointManager.SecurityProtocol = SecurityProtocolType.Ssl3 | SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
            var responese = new MaptqCN.ws().runWSobj(new MaptqCN.wsRequestObj()
            {
                CandidateId = app.CadidateId,
                LastName = FirstName,
                FirstName = LastName,
                ClientId = "10000010",
                ProjectId = "10000383",
                SecureCode = "f97c3ded78e44adf18bc35780916b6df",
                ReturnUrl = "https://app.askform.cn/Custom/MSDReport/SHDTCallBack-v3.ashx",
                EmailAddress = Email,
                RequestType = "page_candhub_autoregister",
                LanguageId = 19,
                NormsetId = NormsetId
            });

            //只有接口请求成功的时候才修改状态
            if (responese.ErrorCode == 0)
            {
                //添加Entry
                EntryID = AskForm_Entry_Insert();

                AddValueForFieldName(EntryID, NewSetting.ApplicantIdFieldID, app.ApplicantId.ToString());
                AddValueForFieldName(EntryID, NewSetting.NameFieldID, ItemObj.Value);
                AddValueForFieldName(EntryID, NewSetting.CadidateIdFieldID, app.CadidateId);
                AddValueForFieldName(EntryID, NewSetting.EmailFieldID, Email);
                AddValueForFieldName(EntryID, NewSetting.JobIdFieldID, jobid);
                AddValueForFieldName(EntryID, NewSetting.JobId2FieldID, jobid2);
                AddValueForFieldName(EntryID, NewSetting.NormsetIdFieldID, NormsetId);
                //加入邮件消息队列
                AddEmailSendList(EntryID, Email, responese.RedirectUrl, ItemObj.Value);

                Console.WriteLine("编号:" + id + " ,姓名:" + ItemObj.Value + " ,邮箱:" + EmailObj.Value);
            }

            SignJobToU15(app.ApplicantId, jobid2);
        }

        /// <summary>
        /// 发送邮件
        /// </summary>
        /// <param name="EntryID"></param>
        /// <param name="Email"></param>
        /// <param name="TestHref"></param>
        /// <param name="UserName"></param>
        /// <param name="SendAgn"></param>
        private static void AddEmailSendList(long EntryID, string Email, string TestHref, string UserName)
        {
            var ec = NewSetting.EmailContent.Replace("{LinkHref}", TestHref).Replace("{UserName}", UserName).Replace("'", "\"");
            string InsertSQL = $@"Insert into AskForm_EmailSendList(Title,Content,Recipient,CC,BCC,DisplayName,SMTPID,
                    Attachment,SentCount,AllowedCount,Status,Result,EntryID,EmailServerUrlID,FormID,FormApplicationID,
                    UserID,CompanyID,IsDeleted,StartTime,CreatedDate,ModifiedDate,SendPriority,IsIdleSend)
                    Values('上汽大通-测评通知','{ec}','{Email}','','','上汽大通',211,'',1,2,1,'',{EntryID},53,{NewSetting.FormID},{NewSetting.FormApplicationID},0,{NewSetting.CompanyID},0,GETDATE(),GETDATE(),GETDATE(),0,0)";

            DB.ExecuteNonQuery(InsertSQL, true);
        }


        /// <summary>
        /// 重新发送邮件
        /// </summary>
        /// <param name="Email"></param>
        private static void UpdateEmailToSending(string Email)
        {
            var sql = $@"update AskForm_EmailSendList set Status=1 where Recipient='{Email}' and IsDeleted=0 and FormID={NewSetting.FormID}";
            DB.ExecuteNonQuery(sql, true);
        }

        private static int SelectCountByEmail(string Email)
        {
            var sql = $@"select count(*) from AskForm_EmailSendList where Recipient='{Email}' and IsDeleted=0 and FormID={NewSetting.FormID}";
            return Convert.ToInt32(DB.ExecuteScalar(sql, true));
        }


        /// <summary>
        /// 为AskForm_Entry 主表添加数据
        /// </summary>
        /// <returns></returns>
        private static long AskForm_Entry_Insert()
        {
            //return new FormEntry()
            // {
            //     CompanyID = NewSetting.CompanyID,
            //     FormApplicationID = NewSetting.FormApplicationID,
            //     FormID = NewSetting.FormID,
            //     FormPageID = 0,
            //     Version = 0,
            //     UserID = 0,
            //     TotalPoint = 0,
            //     Status = EntryStatus.Added,
            //     ModifiedDate = DateTime.Now,
            //     CreatedDate = DateTime.Now,
            //     CreatedUserID = 0,
            // }.InsertUpdate();

            string SQL = $@"Insert Into AskForm_Entry
            (FormID,CityID,TotalPoint,AllowedVersion,Version,Duration,BigIntIP,FormPageID,SourceType,Status,Reason
            ,IsArchived,CreatedUserID,ModifiedUserID,FormApplicationID,CompanyID,IsDeleted,CreatedDate,ModifiedDate,SendStatus,SendDate) 
            Values
            ({NewSetting.FormID},0,0,0,0,0,0,{NewSetting.FormPageID},0,0,'',0,0,0,{NewSetting.FormApplicationID},{NewSetting.CompanyID},0,GETDATE(),GETDATE(),1,GETDATE())
            SELECT @@IDENTITY AS ID";
            var Result = Convert.ToInt64(DB.ExecuteScalar(SQL));
            return Result;
        }

        /// <summary>
        /// 为EntryText添加数据  根据EntryID、FieldID、Value
        /// </summary>
        /// <param name="EntryID"></param>
        /// <param name="FieldID"></param>
        /// <param name="Value"></param>
        private static void AddValueForFieldName(long EntryID, long FieldID, string Value)
        {
            //var entryTextID = new EntryText()
            //{
            //    EntryID = EntryID,
            //    FieldID = FieldID,
            //    Value = Value,
            //    CompanyID = NewSetting.CompanyID,
            //    FormApplicationID = NewSetting.FormApplicationID,
            //    FormID = NewSetting.FormID,
            //    CreatedDate = DateTime.Now,
            //    CreatedUserID = 0,
            //    ModifiedUserID = 0
            //}.InsertUpdate();

            DB.ExecuteNonQuery($@"Insert Into AskForm_EntryText(EntryID, FieldID, Value, Point, FormID, FormApplicationID, CompanyID, IsDeleted, CreatedDate, ModifiedDate, CreatedUserID, ModifiedUserID)
                Values({EntryID},{FieldID},'{Value}',0,{NewSetting.FormID},{NewSetting.FormApplicationID},{NewSetting.CompanyID},0,getdate(),getdate(),0,0)");
        }


        /// <summary>
        /// 查找是否已做过测评 (使用AppID查询到EntryID.使用EntryID查询测评分数)
        /// </summary>
        public static bool IsTested(string CandID, out long EntryID)
        {
            EntryID = 0;
            var Sql = $@"select EntryID,Version from AskForm_Entry where IsDeleted=0 and EntryID=(select top 1 EntryID from AskForm_EntryText 
            where IsDeleted=0 and FieldID={NewSetting.CadidateIdFieldID} and Value='{CandID}' Order By CreatedDate Desc)";

            var dt = DB.ExecuteQuery(Sql);
            if (dt.Rows.Count != 0)
            {
                EntryID = Convert.ToInt64(dt.Rows[0]["EntryID"]);
                //当大于等于4的时候  需要根据candid重新生成对应的报告
                return Convert.ToInt32(dt.Rows[0]["Version"]) >= 2;
            }
            return false;
        }

        /// <summary>
        /// 如果已经测评的AppID  那么直接模拟Post  Callback
        /// </summary>
        public static void SimulationExec(string CadidateId)
        {
            var url = "https://app.askform.cn/Custom/MSDReport/SHDTCallBack-v3.ashx";
            var parms = $"instr=1&cand={CadidateId}&valid=1";
            HttpHelper.HttpPost(url, parms);
        }

        /// <summary>
        /// 更新用户的JOBID
        /// </summary>
        private static void UpdateJobID(long EntryID, long JobID)
        {
            var Sql = $@"update AskForm_EntryText set Value='{JobID}' where EntryID={EntryID} and FormID={NewSetting.FormID} and 
            IsDeleted=0 and FieldID={NewSetting.JobIdFieldID}";
            DB.ExecuteNonQuery(Sql);
        }

        /// <summary>
        /// 将简历标记为测评进行中
        /// </summary>
        private static void SignJobToU15(string Applicantid, string Jobid)
        {
            var Url = $"http://api.beisenapp.com/recruitv2/{NewSetting.Tenant_ID}/applicant/contojoborstorsDb?format=json";

            Dictionary<string, string> dicParameter = new Dictionary<string, string>() {
                    {"applicant_ids",Applicantid},
                    {"job_id",Jobid},
                    {"phase_id","S02" },
                    {"status_id","U015" }
            };
            string resultjson = HttpHelper.HttpPost(Url, dicParameter);
            Result result = JsonConvert.DeserializeObject<Result>(resultjson);
        }





        private static string GetJobID(string str)
        {
            if (string.IsNullOrEmpty(str)) return "0";

            if (str.IndexOf("研发序列") >= 0)
            {
                return "18110205";
            }
            else if (str.IndexOf("供应链序列") >= 0)
            {
                return "18110201";
            }
            else if (str.IndexOf("IT序列") >= 0)
            {
                return "18110202";
            }
            else if (str.IndexOf("职能序列") >= 0)
            {
                return "18110203";
            }
            else if (str.IndexOf("制造序列") >= 0)
            {
                return "18110204";
            }
            else if (str.IndexOf("营销序列") >= 0)
            {
                return "18110206";
            }
            return "0";
        }
    }
}
