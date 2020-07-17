namespace Sparkle.LinkedInNET.DemoMvc5.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Web;
    using System.Web.Mvc;
    using Ninject;
    using Sparkle.LinkedInNET.DemoMvc5.Domain;
    using Sparkle.LinkedInNET.OAuth2;
    using Sparkle.LinkedInNET.Profiles;
    using System.Threading.Tasks;
    using Sparkle.LinkedInNET.Organizations;
    using System.Net;
    using System.Text.RegularExpressions;
    using Sparkle.LinkedInNET.Common;
    using System.Drawing;
    using System.Drawing.Imaging;
    using System.Drawing.Drawing2D;
    using Sparkle.LinkedInNET.DemoMvc5.Utils;
    using Sparkle.LinkedInNET.SocialActions;
    using Sparkle.LinkedInNET.UGCPost;
    using Sparkle.LinkedInNET.Video;

    ////using Sparkle.LinkedInNET.ServiceDefinition;

    public class HomeController : Controller
    {
        private LinkedInApi api;
        private DataService data;
        private LinkedInApiConfiguration apiConfig;

        public HomeController(LinkedInApi api, DataService data, LinkedInApiConfiguration apiConfig)
        {
            this.api = api;
            this.data = data;
            this.apiConfig = apiConfig;
        }

        public async Task<ActionResult> Index(string culture = "en-US")
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            // step 1: configuration
            this.ViewBag.Configuration = this.apiConfig;

            // step 2: authorize url
            var scope = AuthorizationScope.ReadEmailAddress | AuthorizationScope.ReadWriteCompanyPage | AuthorizationScope.WriteShare;
            var state = Guid.NewGuid().ToString();
            var redirectUrl = this.Request.Compose() + this.Url.Action("OAuth2");
            this.ViewBag.LocalRedirectUrl = redirectUrl;
            if (this.apiConfig != null && !string.IsNullOrEmpty(this.apiConfig.ApiKey))
            {
                var authorizeUrl = this.api.OAuth2.GetAuthorizationUrl(scope, state, redirectUrl);
                this.ViewBag.Url = authorizeUrl;
            }
            else
            {
                this.ViewBag.Url = null;
            }

            var accessToken = "";            
            this.data.SaveAccessToken(accessToken);


            // step 3
            if (this.data.HasAccessToken)
            {
                var token = this.data.GetAccessToken();
                this.ViewBag.Token = token;
                var user = new UserAuthorization(token);

                var watch = new Stopwatch();
                watch.Start();
                try
                {
                    ////var profile = this.api.Profiles.GetMyProfile(user);
                    var acceptLanguages = new string[] { culture ?? "en-US", "fr-FR", };
                    var fields = FieldSelector.For<Person>().WithAllFields();
                    var profile = await this.api.Profiles.GetMyProfileAsync(user, acceptLanguages, fields);

                    //await CreateLike(user);
                    //await GetProfile(user);
                    //await GetPosts(user);
                    //await GetComemnt(user);
                    // await GetPost(user);
                    // await GetPosts(user);
                    //await GetVideo(user);
                    //await PublishImage(user);
                    await PublishTest(user);


                    // var originalPicture = await this.api.Profiles.GetOriginalProfilePictureAsync(user);
                    // this.ViewBag.Picture = originalPicture;

                    //this.ViewBag.Profile = profile;
                }
                catch (LinkedInApiException ex)
                {
                    this.ViewBag.ProfileError = ex.ToString();
                }
                catch (Exception ex)
                {
                    this.ViewBag.ProfileError = ex.ToString();
                }

                watch.Stop();
                this.ViewBag.ProfileDuration = watch.Elapsed;
            }

            return this.View();
        }

        public async Task<ActionResult> OAuth2(string code, string state, string error, string error_description)
        {
            if (!string.IsNullOrEmpty(error))
            {
                this.ViewBag.Error = error;
                this.ViewBag.ErrorDescription = error_description;
                return this.View();
            }

            var redirectUrl = this.Request.Compose() + this.Url.Action("OAuth2");
            var result = await this.api.OAuth2.GetAccessTokenAsync(code, redirectUrl);

            this.ViewBag.Code = code;
            this.ViewBag.Token = result.AccessToken;

            this.data.SaveAccessToken(result.AccessToken);

            var user = new UserAuthorization(result.AccessToken);

            ////var profile = this.api.Profiles.GetMyProfile(user);
            ////this.data.SaveAccessToken();
            return this.View();
        }

        public ActionResult Connections()
        {
            var token = this.data.GetAccessToken();
            var user = new UserAuthorization(token);
            // var connection = this.api.Profiles.GetMyConnections(user, 0, 500);
            return this.View(string.Empty);
        }

        public ActionResult FullProfile(string id, string culture = "en-US")
        {
            var token = this.data.GetAccessToken();
            this.ViewBag.Token = token;
            var user = new UserAuthorization(token);

            Person profile = null;
            var watch = new Stopwatch();
            watch.Start();
            try
            {
                ////var profile = this.api.Profiles.GetMyProfile(user);
                var acceptLanguages = new string[] { culture ?? "en-US", "fr-FR", };
                var fields = FieldSelector.For<Person>()
                    .WithAllFields();
                profile = this.api.Profiles.GetMyProfileAsync(user, acceptLanguages, fields).Result;

                this.ViewBag.Profile = profile;
            }
            catch (LinkedInApiException ex)
            {
                this.ViewBag.ProfileError = ex.ToString();
                this.ViewBag.RawResponse = ex.Data["ResponseText"];
            }
            catch (LinkedInNetException ex)
            {
                this.ViewBag.ProfileError = ex.ToString();
                this.ViewBag.RawResponse = ex.Data["ResponseText"];
            }
            catch (Exception ex)
            {
                this.ViewBag.ProfileError = ex.ToString();
            }

            watch.Stop();
            this.ViewBag.ProfileDuration = watch.Elapsed;

            return this.View(profile);
        }

        public ActionResult Play()
        {
            var token = this.data.GetAccessToken();
            this.ViewBag.Token = token;
            return this.View();
        }

        public ActionResult Definition()
        {
            var filePath = Path.Combine(this.Server.MapPath("~"), "..", "LinkedInApiV2.xml");
            var builder = new Sparkle.LinkedInNET.ServiceDefinition.ServiceDefinitionBuilder();
            using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                builder.AppendServiceDefinition(fileStream);
            }

            var result = new ApiResponse<Sparkle.LinkedInNET.ServiceDefinition.ApisRoot>(builder.Root);


            MemoryStream stream = new MemoryStream();
            StreamWriter writer = new System.IO.StreamWriter(stream);
            var generator = new ServiceDefinition.CSharpGenerator(writer);
            generator.Run(builder.Definition);
            stream.Seek(0L, SeekOrigin.Begin);
            var serviceResult = new StreamReader(stream).ReadToEnd();


            return this.Json(result, JsonRequestBehavior.AllowGet);
        }

        public ActionResult LogOff(string ReturnUrl)
        {
            this.data.ClearAccessToken();

            if (this.Url.IsLocalUrl(ReturnUrl))
            {
                return this.Redirect(ReturnUrl);
            }
            else
            {
                return this.RedirectToAction("Index");
            }
        }

        public class ApiResponse<T>
        {
            public ApiResponse()
            {
            }

            public ApiResponse(T data)
            {
                this.Data = data;
            }

            public string Error { get; set; }
            public T Data { get; set; }
        }

        private async Task PublishTest(UserAuthorization user)
        {
            //// var getVideo = await this.api.Asset.GetAssetAsync(user, "C4D05AQH5Hen4KpIFqA");


            //// var videoData = DownladFromUrlToByte("https://c3labsdevstorage.blob.core.windows.net/7e46a98d-a143-4a4d-8e05-b3f95493cce4/e21b6488-8d6e-43e6-8c88-4ac4438ff8cb/videos/48c2071e-e9b0-43f7-8b8a-01da4d8c04a1.mp4");
            //var videoData1 = DownladFromUrlToByte("https://c3labsdevstorage.blob.core.windows.net/7e46a98d-a143-4a4d-8e05-b3f95493cce4/e21b6488-8d6e-43e6-8c88-4ac4438ff8cb/videos/f57f8bc8-4fc8-44e4-9c5f-40f528f1a295.mp4");

            //// var bigVideo = DownladFromUrlToByte("https://c3labsdevstorage.blob.core.windows.net/edf54915-d374-4074-a8ee-196897a7badd/07569e4a-134f-48ec-96cf-c89dd1234e9b/videos/152d90cb-3501-4673-a5b2-1ff61ecc9d33.mpeg");


            //var aaa = await Video.VideoUpload.UploadVideoAsync(api, user, "urn:li:organization:18568129", videoData1);
            //var bb = "";




            //// Asset test
            //var asset = new Asset.RegisterUploadRequest()
            //{
            //    RegisterUploadRequestData = new Asset.RegisterUploadRequestData()
            //    {

            //        // fileSizeIn bytes
            //        FileSize = 52429800,
            //        SupportedUploadMechanism = new List<string>() { "MULTIPART_UPLOAD" },
            //        // SupportedUploadMechanism = new List<string>() { "SINGLE_REQUEST_UPLOAD" },
            //        // Owner = "urn:li:person:" + "qhwvZ0K4cr",
            //        Owner = "urn:li:organization:18568129",
            //        Recipes = new List<string>() { "urn:li:digitalmediaRecipe:feedshare-video" },
            //        ServiceRelationships = new List<Asset.ServiceRelationship>()
            //        {
            //            new Asset.ServiceRelationship()
            //            {
            //                Identifier = "urn:li:userGeneratedContent",
            //                RelationshipType = "OWNER"
            //            }
            //        }
            //    }
            //};
            //var requestAsset = await this.api.Asset.RegisterUploadAsync(user, asset);


            //var multiPartSend = await Internals.LongVideoUpload.UploadLongVideoPartsAsync(this.api, requestAsset, bigVideo);

            ////var postAsset = await this.api.Asset.UploadAssetAsync(requestAsset.Value.UploadMechanism.ComLinkedinDigitalmediaUploadingMediaUploadHttpRequest.UploadUrl, new Asset.UploadAssetRequest()
            ////{
            ////    RequestHeaders = new Asset.ComLinkedinDigitalmediaUploadingMediaUploadHttpRequest()
            ////    {
            ////        Headers = requestAsset.Value.UploadMechanism.ComLinkedinDigitalmediaUploadingMediaUploadHttpRequest.Headers,
            ////        UploadUrl = requestAsset.Value.UploadMechanism.ComLinkedinDigitalmediaUploadingMediaUploadHttpRequest.UploadUrl,
            ////    },
            ////    Data = videoData1
            ////});

            //var test = "sss";


             var mediaUrl = await UploadVideo(user);

            // video test
            var ugcPost = new UGCPost.UGCPostData()
            {
                // Author = "urn:li:person:" + "qhwvZ0K4cr",
                // Author = "urn:li:organization:" + "18568129",
                Author = "urn:li:organization:18568129",
                LifecycleState = "PUBLISHED",
                SpecificContent = new UGCPost.SpecificContent()
                {
                    ComLinkedinUgcShareContent = new UGCPost.ComLinkedinUgcShareContent()
                    {
                        UGCMedia = new List<UGCPost.UGCMedia>()
                                {
                                    new UGCPost.UGCMedia()
                                    {
                                        UGCMediaDescription = new UGCPost.UGCText()
                                        {
                                            Text = "test description"
                                        },
                                        Media = mediaUrl,// requestAsset.Value.Asset, // "urn:li:digitalmediaAsset:C4D05AQHwsp8DLpxHiA", // "urn:li:digitalmediaAsset:C5500AQG7r2u00ByWjw",
                                        Status = "READY",
                                        // Thumbnails = new List<string>(),
                                        UGCMediaTitle = new UGCPost.UGCText()
                                        {
                                            Text = "Test Title"
                                        }
                                    }
                                },
                        ShareCommentary = new UGCPost.UGCText()
                        {
                            Text = "Test Commentary 111"
                        },
                        ShareMediaCategory = "VIDEO"
                    }
                },
                //TargetAudience = new Common.TargetAudience()
                //{

                //},
                Visibility = new UGCPost.UGCPostvisibility()
                {
                    comLinkedinUgcMemberNetworkVisibility = "PUBLIC"
                }
            };

            var ugcPostResult = await this.api.UGCPost.PostAsync(user, ugcPost);

            //var test2 = "sdfas";

















            //// image test
            //// var imageData = DownladFromUrlToByte("https://c3labsdevstorage.blob.core.windows.net/7e46a98d-a143-4a4d-8e05-b3f95493cce4/e21b6488-8d6e-43e6-8c88-4ac4438ff8cb/images/83278b25-b809-4458-912b-55b4d6d8b19d.jpg");
            //var imageData = DownladFromUrlToByte("https://c3labsdevstorage.blob.core.windows.net/7e46a98d-a143-4a4d-8e05-b3f95493cce4/e21b6488-8d6e-43e6-8c88-4ac4438ff8cb/images/b7b12f6e-4eed-4ca1-b937-006b0c2aa93b.jpg");

            //var postId = this.api.Media.Post(user, new Common.MediaUploadData()
            //{
            //    Data = imageData
            //});

            //var test = "sdfas";













            // var profile1 =  this.api.Profiles.GetMyProfile(user, acceptLanguages, fields);


            //var firstName = profile.FirstName.Localized.First.ToObject<string>();
            //var firstName1 = profile.FirstName.Localized.First.Last.ToString();
            //var firstName2 = profile.FirstName.Localized.First.ToObject<string>();

            //var fieldsOrg = FieldSelector.For<OrganizationalEntityAcls>()
            //    .WithAllFields();
            //var userCompanies = this.api.Organizations.GetUserAdminApprOrganizations(user, fieldsOrg);


            //var statistic = this.api.Shares.GetShareStatistics(user, "18568129", "6386953337324994560");

            //var orgFollorerStatistic = this.api.Organizations.GetOrgFollowerStatistics(user, "18568129");

            //var getShares = this.api.Shares.GetShares(user, "urn:li:organization:18568129", 1000, 5, 0);

            //var postResult = this.api.Shares.Post(user, new Common.PostShare()
            //{
            //    Content = new Common.PostShareContent()
            //    {
            //        Title = "tttt",
            //        ContentEntities = new List<Common.PostShareContentEntities>() { new Common.PostShareContentEntities() {
            //                  //EntityLocation = "https://www.example.com/",
            //                  //Thumbnails = new List<Common.PostShareContentThumbnails>(){new Common.PostShareContentThumbnails()
            //                  //{
            //                  //    ResolvedUrl = "http://wac.2f9ad.chicdn.net/802F9AD/u/joyent.wme/public/wme/assets/ec050984-7b81-11e6-96e0-8905cd656caf.jpg?v=30"
            //                  //} }
            //                  Entity = postId.Location
            //              }
            //          }
            //    },
            //    Distribution = new Common.Distribution()
            //    {
            //        LinkedInDistributionTarget = new Common.LinkedInDistributionTarget()
            //        {
            //            VisibleToGuest = true
            //        }
            //    },
            //    Subject = "sub",
            //    Text = new Common.PostShareText()
            //    {
            //        Text = "text"
            //    },
            //    // Owner = "urn:li:person:" + "123456789"
            //    Owner = "urn:li:organization:18568129",
            //}
            //);
        }

        private async Task PublishImage(UserAuthorization user)
        {
            var imageUrl = "https://c3labsdevstorage.blob.core.windows.net/7e46a98d-a143-4a4d-8e05-b3f95493cce4/e21b6488-8d6e-43e6-8c88-4ac4438ff8cb/images/e0858668-005b-4f6c-a0b5-b0695661be6b.png";
            var imageData = await ImageUtils.PrepareImageFromUrl(imageUrl, 10485760);

            var postId = await this.api.Media.PostAsync(user, new Common.MediaUploadData()
            {
                Data = imageData
            });
        }

        private async Task<string> UploadVideo(UserAuthorization user)
        {
            var videoUrl = "https://diricostorage.blob.core.windows.net/48131979-e14a-47c4-bf19-e02bb7226732/cb07d670-6fc8-410f-b21e-3456a284f523/4cf54e39-afdb-47b1-bf7d-020464985554/8b896ee0-d73d-48bc-bc92-60ce09407028/data";
            var videoData = await ImageUtils.PrepareImageFromUrl(videoUrl, 10485760);

            var ownerUrn = "urn:li:organization:18568129";
            var videoUploadUrn = await VideoUpload.UploadVideoAsync(api, user, ownerUrn, videoData);
            
            return videoUploadUrn;            
        }

        private async Task GetComemnt(UserAuthorization user)
        {
            try
            {
                //// postId
                var comments = await this.api.SocialActions.GetCommentsByUrnAsync(user, "urn:li:comment:(urn:li:activity:6604993552747380736,6604995118833377280)");

                var comment = await this.api.SocialActions.GetCommentsByUrnAsync(user, "urn:li:organization:18568129");

                await ReplyComment(user, comment.Elements.First());
                //var deleteLike = this.api.SocialActions.DeleteComment(user, comment.Elements.First().Urn, comment.Elements.First().Id, comment.Elements.First().Actor);
            }
            catch (Exception ex)
            {

            }
        }

        private async Task ReplyComment(UserAuthorization user, CommentResult comment)
        {
            try
            {
                var actorUrn = comment.Actor;

                Random r = new Random();
                var createCommentRequest = new CreateCommentRequest
                {
                    Actor = actorUrn,
                    Message = new CommentMessage
                    {
                        Text = "test 1" + r.Next()
                    },
                    ParentComment = comment.Urn
                };

                var response = await this.api.SocialActions.CreateCommentOnUrnAsync(user,
                    comment.Urn, createCommentRequest);
            }
            catch (Exception ex)
            {

            }
        }
        private async Task GetVideo(UserAuthorization user)
        {
            var videoId = "urn:li:digitalmediaAsset:C4E05AQHDEiyF00wWkQ"; // video post on Horvath janos zrt
            var postId = "urn:li:ugcPost:6650705316675665920"; // video post on Horvath janos zrt.            
            var video = await this.api.UGCPost.GetUGCVideoAsync(user, postId);
            var mediaElements = video.SpecificContent.ComLinkedinUgcVideoContent.UGCMedia[0].mediaData.Elements;

            var videoUrl = mediaElements.FirstOrDefault().Identifiers[0].Identifier;

        }

        private async Task GetPost(UserAuthorization user)
        {
            var postId = "urn:li:ugcPost:6650705316675665920"; // video post on Horvath janos zrt.
            var post = await this.api.UGCPost.GetUGCPostAsync(user, postId);
        }

        private async Task GetPosts(UserAuthorization user)
        {
            var pageId = "urn:li:organization:18568129"; // Horvath janos zrt.            
            var post = await this.api.UGCPost.GetUGCPostsAsync(user, pageId, 0, 5);
            // await DeletePost(user, post.Elements.Last());
        }

        private async Task DeletePost(UserAuthorization user, UGCPostItemResult post)
        {
            try
            {

                var response = await this.api.UGCPost.DeleteUGCPostAsync(user, post.Id);
            }
            catch (Exception ex)
            {

            }
        }

        private async Task GetProfile(UserAuthorization user)
        {

            try
            {
                // var profile = await this.api.Profiles.GetProfileAsync(user, "LWq7hpOmwk");
                var profile = await this.api.Profiles.GetProfileAsync(user, "1ky82GzXRL");
                var profiles = await this.api.Profiles.GetProfilesByIdsAsync(user, "(id:qhwvZ0K4cr),(id:LWq7hpOmwk),(id:1ky82GzXRL)");
            }
            catch { }
        }

        private async Task CreateLike(UserAuthorization user)
        {
            try
            {
                var postUrn = "urn:li:comment:(urn:li:activity:6604993552747380736,6605016542239301632)";
                var actorUrn = "urn:li:organization:18568129";

                var createLikeRequest = new CreateLikeRequest
                {
                    Actor = actorUrn,
                    Object = postUrn
                };

                var like = await this.api.SocialActions.CreateLikeAsync(user, postUrn, createLikeRequest);
                await this.api.SocialActions.DeleteLikeAsync(user, postUrn, actorUrn, actorUrn);
            }
            catch (Exception ex)
            {

            }
        }
    }
}
