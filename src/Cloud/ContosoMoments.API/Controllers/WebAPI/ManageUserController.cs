﻿using Microsoft.Azure.Mobile.Server.Authentication;
using Microsoft.Azure.Mobile.Server.Config;
using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;

namespace ContosoMoments.Api
{
    [MobileAppController]
    //[Authorize]
    public class ManageUserController : ApiController
    {
        protected const string FacebookGraphUrl = "https://graph.facebook.com/v2.5/me?fields=email%2Cfirst_name%2Clast_name&access_token=";

        internal static async Task<string> GetUserId(HttpRequestMessage request, IPrincipal user)
        {
            string result = "";

            // Get the credentials for the logged-in user.
            var fbCredentials = await user.GetAppServiceIdentityAsync<FacebookCredentials>(request);
            var aadCredentials = await user.GetAppServiceIdentityAsync<AzureActiveDirectoryCredentials>(request);

            if (fbCredentials?.Claims?.Count > 0)
            {
                result = CheckAddEmailToDB(fbCredentials.UserId);
            }
            else if (aadCredentials?.Claims?.Count > 0)
            {
                result = CheckAddEmailToDB(aadCredentials.UserId);
            }

            return UserOrDefault(result);
        }

        // GET api/ManageUser
        public async Task<string> Get()
        {
            return await GetUserId(Request, User);
        }

        private static string UserOrDefault(string retVal)
        {
            if (string.IsNullOrWhiteSpace(retVal))
            {
                retVal = new ConfigModel().DefaultUserId;
            }

            return retVal;
        }

        private static string CheckAddEmailToDB(string email)
        {
            var identifier = GenerateHashFromEmail(email);

            using (var ctx = new MobileServiceContext())
            {
                var user = ctx.Users.FirstOrDefault(x => x.Email == identifier);

                // user was found, return it
                if (user != default(Common.Models.User))
                {
                    return user.Id;
                }

                // create new user
                return AddUser(identifier, ctx);
            }
        }

        private static string AddUser(string emailHash, MobileServiceContext ctx)
        {
            var u = ctx.Users.Add(
                new Common.Models.User
                {
                    Id = Guid.NewGuid().ToString(),
                    Email = emailHash,
                    IsEnabled = true
                });

            ctx.SaveChanges();

            return u.Id;
        }

        /// <summary>
        /// We are using Hashes so that we do not store PII.
        /// </summary>
        /// <param name="email">Real email address coming from Identity Provider</param>
        /// <returns>SHA256 Hash of email to ensure privacy</returns>
        private static string GenerateHashFromEmail(string email)
        {
            StringBuilder hashString = new StringBuilder();

            using (var generator = System.Security.Cryptography.SHA256.Create())
            {
                var emailBytes = Encoding.UTF8.GetBytes(email);
                var hash = generator.ComputeHash(emailBytes);

                foreach (var b in hash)
                {
                    hashString.AppendFormat("{0:x2}", b);
                }
            }

            return hashString.ToString();
        }
    }
}
