using contentapi.Controllers;
using Xunit;
using contentapi.Models;
using System;
using contentapi.Services;
using System.Collections.Generic;
using System.Linq;

namespace contentapi.test
{
    public class UserControllerTest : ControllerTestBase<UsersController>
    {
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void TestBasicUserCreate(bool loggedIn)
        {
            var instance = GetInstance(loggedIn);
            var credential = GetNewCredentials();
            var userResult = instance.Controller.PostCredentials(credential).Result;
            Assert.True(userResult.Value.username == credential.username);
            Assert.True(userResult.Value.id > 0);
            Assert.True(userResult.Value.createDate <= DateTime.Now);
            Assert.True(userResult.Value.createDate > DateTime.Now.AddDays(-1));
        }

        private void TestUserCreateDupe(ControllerInstance<UsersController> instance, Action<UserCredential> alterCredential)
        {
            var credential = GetNewCredentials();
            var userResult = instance.Controller.PostCredentials(credential).Result;
            Assert.True(userResult.Value.username == credential.username);
            alterCredential(credential);
            userResult = instance.Controller.PostCredentials(credential).Result;
            Assert.True(IsBadRequest(userResult.Result));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void TestUserCreateDupeUsername(bool loggedIn)
        {
            TestUserCreateDupe(GetInstance(loggedIn), (c) => c.email += "a");
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void TestUserCreateDupeEmail(bool loggedIn)
        {
            TestUserCreateDupe(GetInstance(loggedIn), (c) => c.username += "a");
        }

        [Fact]
        public void TestUserMe()
        {
            var instance = GetInstance(true);
            var result = instance.Controller.Me().Result;
            Assert.Equal(instance.User.id, result.Value.id);
            Assert.Equal(instance.User.username, result.Value.username);
        }

        [Fact]
        public void TestUserMeLoggedOut()
        {
            var instance = GetInstance(false);
            var result = instance.Controller.Me().Result;
            Assert.True(IsBadRequest(result.Result) || IsNotFound(result.Result)); //This may not always be a bad request!
            Assert.Null(result.Value);
        }

        [Fact]
        public void TestUserSelfDeleteFail()
        {
            //I don't care WHO we are, we can't delete!
            var instance = GetInstance(true);
            var result = instance.Controller.Delete(instance.User.id).Result;
            Assert.False(IsSuccessRequest(result));
        }

        [Fact]
        public void TestGetUsers()
        {
            var instance = GetInstance(true);
            var result = instance.Controller.Get(new CollectionQuery()).Result;
            Assert.True(IsSuccessRequest(result));
            List<UserView> users = ((IEnumerable<UserView>)result.Value["collection"]).ToList();
            Assert.True(users.Count > 0);
            Assert.Contains(users, x => x.id == instance.User.id);
        }

        [Fact]
        public void TestGetUserSingle()
        {
            var instance = GetInstance(true);
            var result = instance.Controller.GetSingle(instance.User.id).Result;
            Assert.True(IsSuccessRequest(result));
            Assert.True(result.Value.id == instance.User.id);
        }

        /*[Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void TestUserAuthenticate(bool loggedIn)
        {
            var instance = GetInstance(loggedIn);
            var result = instance.Controller.Authenticate(instance.User).Result;
            Assert.True(context.IsSuccessRequest(result));
        }

        [Fact]
        public void TestRandomDeleteFail()
        {
            context.Login();
            var creds = context.GetNewCredentials();
            var newUser = controller.PostCredentials(creds).Result;
            Assert.True(newUser.Value.id > 0); //Just make sure a new user was created
            var result = controller.Delete(newUser.Value.id).Result;
            Assert.False(context.IsSuccessRequest(result));
        }*/

    }
}