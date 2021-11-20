using System;
using System.Data;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using AutoMapper;
using contentapi.Db;
using contentapi.Services.Extensions;
using contentapi.Services.Implementations;
using Dapper;
using Dapper.Contrib.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;
using contentapi.Services.Constants;
using contentapi.Views;
using System.Collections.Generic;

namespace contentapi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class NewConvertController : Controller
    {
        protected  ILogger logger;
        protected  UserViewSource userSource;
        protected  BanViewSource banSource;
        protected  ModuleViewSource moduleSource;
        protected  FileViewSource fileSource;
        protected  ContentViewSource contentSource;
        protected  CategoryViewSource categorySource;
        protected  VoteViewSource voteSource;
        protected  WatchViewSource watchSource;
        protected  CommentViewSource commentSource;
        protected  ActivityViewSource activitySource;
        //protected  ContentApiDbContext ctapiContext;
        protected IMapper mapper;
        protected IDbConnection newdb;
        protected IEntityProvider entityProvider;


        public NewConvertController(ILogger<NewConvertController> logger, UserViewSource userSource, BanViewSource banSource,
            ModuleViewSource moduleViewSource, FileViewSource fileViewSource, ContentViewSource contentViewSource, 
            CategoryViewSource categoryViewSource, VoteViewSource voteViewSource, WatchViewSource watchViewSource, 
            CommentViewSource commentViewSource, ActivityViewSource activityViewSource, 
            ContentApiDbConnection cdbconnection, IEntityProvider entityProvider,
            /*ContentApiDbContext ctapiContext,*/ IMapper mapper)
        {
            this.logger = logger;
            this.userSource = userSource;
            this.banSource = banSource;
            this.moduleSource = moduleViewSource;
            this.fileSource = fileViewSource;
            this.contentSource = contentViewSource;
            this.categorySource = categoryViewSource;
            this.voteSource = voteViewSource;
            this.watchSource = watchViewSource;
            this.commentSource = commentViewSource;
            this.activitySource = activityViewSource;
            //this.ctapiContext = ctapiContext;
            this.mapper = mapper;
            this.newdb = cdbconnection.Connection;
            this.entityProvider = entityProvider;
        }


        protected StringBuilder sb = new StringBuilder();

        protected void Log(string message)
        {
            logger.LogInformation(message);
            sb.AppendLine(message);
        }

        protected string DumpLog()
        {
            var result = sb.ToString();
            sb.Clear();
            return result;
        }

        //Includes bans, uservariables
        [HttpGet("users")]
        public async Task<string> ConvertUsersAsync()
        {
            try
            {
                Log("Starting user convert");
                var users = await userSource.SimpleSearchAsync(new UserSearch());
                Log($"{users.Count} users found");
                foreach(var user in users)
                {
                    var newUser = mapper.Map<Db.User>(user);
                    //User dapper to store?
                    var id = await newdb.InsertAsync(newUser);
                    Log($"Inserted user {newUser.username}({id})");
                }
                var count = newdb.ExecuteScalar<int>("SELECT COUNT(*) FROM users");
                Log($"Successfully inserted users, {count} in table");

                Log("Starting ban convert");
                var bans = await banSource.SimpleSearchAsync(new BanSearch());
                Log($"{bans.Count} bans found");
                foreach(var ban in bans)
                {
                    var newban = mapper.Map<Db.Ban>(ban);
                    //User dapper to store?
                    var id = await newdb.InsertAsync(newban);
                    Log($"Inserted ban for {newban.bannedUserId}({id})");
                }
                count = newdb.ExecuteScalar<int>("SELECT COUNT(*) FROM bans");
                Log($"Successfully inserted bans, {count} in table");

                Log("Starting user variable convert");
                //var realKeys = keys.Select(x => Keys.VariableKey + x);

                var evs = await entityProvider.GetQueryableAsync<EntityValue>();
                var ens = await entityProvider.GetQueryableAsync<Entity>();

                var query = 
                    from v in evs
                    where EF.Functions.Like(v.key, $"{Keys.VariableKey}%")
                    //where EF.Functions.Like(v.key, Keys.VariableKey + key) && v.entityId == -uid
                    join e in ens on -v.entityId equals e.id
                    where EF.Functions.Like(e.type, $"{Keys.UserType}%")
                    select v;
                
                var uvars = await query.ToListAsync();
                Log($"{uvars.Count} user variables found");

                foreach(var uvar in uvars)
                {
                    var newvar = new UserVariable()
                    {
                        id = uvar.id,
                        userId = -uvar.entityId ,
                        createDate = uvar.createDate ?? DateTime.Now,
                        editCount = 0,
                        key = uvar.key.Substring(Keys.VariableKey.Length),
                        value = uvar.value
                    };//mapper.Map<Db.Ban>(ban);
                    newvar.editDate = newvar.createDate;
                    var id = await newdb.InsertAsync(newvar);
                    Log($"Inserted uservariable {newvar.key} for {newvar.userId}({id})");
                }

                count = newdb.ExecuteScalar<int>("SELECT COUNT(*) FROM user_variables");
                Log($"Successfully inserted user variables, {count} in table");
            }
            catch(Exception ex)
            {
                Log($"EXCEPTION: {ex}");
            }

            return DumpLog();
        }

        protected async Task<List<long>> ConvertCt<T>(Func<Task<List<T>>> producer, Func<Db.Content, T, Db.Content> modify = null) where T : StandardView
        {
            var ids = new List<long>();
            var tn = typeof(T);
            Log($"Starting {tn.Name} convert");
            var content = await producer();
            //var content = await contentSource.SimpleSearchAsync(new ContentSearch());
            foreach (var ct in content)
            {
                var nc = mapper.Map<Db.Content>(ct);
                nc.deleted = false;
                if(modify != null)
                    nc = modify(nc, ct);
                //User dapper to store?
                var id = await newdb.InsertAsync(nc);
                Log($"Inserted {tn.Name} '{nc.name}'({id})");

                //Now grab the keywords and permissions and values
                var kws = ct.keywords.Select(x => new ContentKeyword()
                {
                    contentId = id,
                    value = x
                }).ToList();
                var lcnt = await newdb.InsertAsync(kws); //IDK if the list version has async
                Log($"Inserted {lcnt} keywords for '{nc.name}'");

                var vls = ct.values.Select(x => new ContentValue()
                {
                    contentId = id,
                    key = x.Key,
                    value = x.Value
                }).ToList();
                lcnt = await newdb.InsertAsync(vls); //IDK if the list version has async
                Log($"Inserted {lcnt} values for '{nc.name}'");

                var pms = ct.permissions.Select(x => new ContentPermission()
                {
                    contentId = id,
                    userId = x.Key,
                    create = x.Value.ToLower().Contains(Actions.KeyMap[Keys.CreateAction].ToLower()),
                    read = x.Value.ToLower().Contains(Actions.KeyMap[Keys.ReadAction].ToLower()),
                    update = x.Value.ToLower().Contains(Actions.KeyMap[Keys.UpdateAction].ToLower()),
                    delete= x.Value.ToLower().Contains(Actions.KeyMap[Keys.DeleteAction].ToLower())
                }).ToList();

                lcnt = await newdb.InsertAsync(pms); //IDK if the list version has async
                Log($"Inserted {lcnt} permissions for '{nc.name}'");

                //And might as well go out and get the watches and votes, since i think
                //those COULD be tied to bad/old content... or something.
                //if(extra != null)
                //    await extra(id);
                ids.Add(id);
            }
            var count = newdb.ExecuteScalar<int>("SELECT COUNT(*) FROM content");
            Log($"Successfully inserted {tn.Name}, {count} in table");
            return ids;
        }

        [HttpGet("content")]
        public async Task<string> ConvertContentAsync()
        {
            try
            {
                var ids = await ConvertCt(() => contentSource.SimpleSearchAsync(new ContentSearch()));

                //Need to get votes and watches ONLY for real content
                Log("Starting vote convert");
                var votes = await voteSource.SimpleSearchAsync(new VoteSearch()
                {
                    ContentIds = ids
                });
                Log($"{votes.Count} votes found");
                foreach(var v in votes)
                {
                    var newVote = mapper.Map<ContentVote>(v);
                     var vt = v.vote.ToLower();
                     if(vt == "b") newVote.vote = VoteType.bad;
                     if(vt == "o") newVote.vote = VoteType.ok;
                     if(vt == "g") newVote.vote = VoteType.good;
                    //User dapper to store?
                    var id = await newdb.InsertAsync(newVote);
                    Log($"Inserted vote {newVote.userId}-{newVote.contentId}({id})");
                }
                var count = newdb.ExecuteScalar<int>("SELECT COUNT(*) FROM content_votes");
                Log($"Successfully inserted votes, {count} in table");

                Log("Starting watch convert");
                var watches = await watchSource.SimpleSearchAsync(new WatchSearch()
                {
                    ContentIds = ids
                });
                Log($"{watches.Count} watches found");
                foreach(var w in watches )
                {
                    var neww = mapper.Map<ContentWatch>(w);
                    //User dapper to store?
                    var id = await newdb.InsertAsync(neww);
                    Log($"Inserted watch {neww.userId}-{neww.contentId}({id})");
                }
                count = newdb.ExecuteScalar<int>("SELECT COUNT(*) FROM content_watches");
                Log($"Successfully inserted watches, {count} in table");

                await ConvertCt(() => fileSource.SimpleSearchAsync(new FileSearch()), (n,o) =>
                {
                    o.values.Add("quantization", o.quantization.ToString());
                    return n;
                });
                await ConvertCt(() => categorySource.SimpleSearchAsync(new CategorySearch()), (n,o) =>
                {
                    o.values.Add("localSupers", string.Join(",", o.localSupers));
                    return n;
                });
            }
            catch(Exception ex)
            {
                Log($"EXCEPTION: {ex}");
            }

            return DumpLog();
        }

        [HttpGet("all")]
        public async Task<string> ConvertAll()
        {
            var sb = new StringBuilder();

            sb.AppendLine(await ConvertUsersAsync());
            sb.AppendLine("---------------");
            sb.AppendLine(await ConvertContentAsync());

            return sb.ToString();
        }
    }
}