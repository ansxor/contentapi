using contentapi.Configs;
using contentapi.Services;
using contentapi.Services.Implementations;
using contentapi.test.Implementations;
//using contentapi.test.Implementations;
using contentapi.Views;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace contentapi.test
{
    //[Collection("ASYNC")]
    public class ModuleViewServiceTests : ServiceConfigTestBase<ModuleViewService, SystemConfig>
    {
        protected SystemConfig sysConfig = new SystemConfig();
        protected UserViewService userService;
        protected UserViewFull superUser;
        protected UserViewFull basicUser;
        protected Requester system = new Requester() { system = true };
        protected Requester super;
        protected Requester basic;

        protected override SystemConfig config => sysConfig;


        public override IServiceCollection CreateServices()
        {
            var result = base.CreateServices();
            result.AddSingleton<IModuleService, FakeModuleService>();
            return result;
        }

        public ModuleViewServiceTests()
        {
            userService = CreateService<UserViewService>();
            superUser = userService.WriteAsync(new UserViewFull() { username = "mysuper" }, system).Result;
            basicUser = userService.WriteAsync(new UserViewFull() { username = "basic" }, system).Result;
            super = new Requester() { userId = superUser.id };
            basic = new Requester() { userId = basicUser.id };
            sysConfig.SuperUsers.Add(superUser.id);
        }

        [Fact]
        public void TestSuperWrite()
        {
            var module = service.WriteAsync(new ModuleView() { name = "test", code = "--wow"}, super).Result;
            Assert.True(module.id > 0);
            Assert.True(module.name == "test");
        }
    
        [Fact]
        public void TestBasicNonWrite()
        {
            AssertThrows<ForbiddenException>(() => service.WriteAsync(new ModuleView() { name = "test", code = "--wow"}, basic).Wait());
        }

        [Fact]
        public void TestSuperDelete()
        {
            var module = service.WriteAsync(new ModuleView() { name = "test", code = "--wow"}, super).Result;
            Assert.True(module.id > 0);
            Assert.True(module.name == "test");
            service.DeleteAsync(module.id, super ).Wait();
        }

        [Fact]
        public void TestNonSuperDelete()
        {
            var module = service.WriteAsync(new ModuleView() { name = "test", code = "--wow"}, super).Result;
            Assert.True(module.id > 0);
            Assert.True(module.name == "test");
            AssertThrows<ForbiddenException>(() => service.DeleteAsync(module.id, basic).Wait());
        }

        [Fact]
        public void TestUpdate()
        {
            var module = service.WriteAsync(new ModuleView() { name = "test", code = "--wow"}, super).Result;
            Assert.True(module.code == "--wow");
            module.code = "--grnadfket";
            var module2 = service.WriteAsync(module, super).Result; //This should NOT throw an exception, using the same id so update
            Assert.True(module.code == "--grnadfket");
        }

        [Fact]
        public void TestUniqueName()
        {
            var module = service.WriteAsync(new ModuleView() { name = "test", code = "--wow"}, super).Result;
            Assert.True(module.name == "test");
            module.id = 0; //Make it a "new" module
            AssertThrows<BadRequestException>(() => service.WriteAsync(module, super).Wait()); //Oops, can't write the same name
        }

        [Fact]
        public void TestDoubleInsert()
        {
            var module = service.WriteAsync(new ModuleView() { name = "test", code = "--wow"}, super).Result;
            Assert.True(module.name == "test");
            var module2 = service.WriteAsync(new ModuleView() { name = "test2", code = "--wow"}, super).Result;
            Assert.True(module2.name == "test2");

            var results = service.SearchAsync(new ModuleSearch(), super).Result;
            Assert.Equal(2, results.Count);
        }

    }
}