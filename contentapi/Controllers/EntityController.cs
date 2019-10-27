using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AutoMapper;
using System.Linq;
using System.Collections.Generic;
using contentapi.Models;
using contentapi.Services;
using Microsoft.Extensions.Logging;

namespace contentapi.Controllers
{
    public class ActionCarryingException : Exception
    {
        public ActionResult Result;

        public ActionCarryingException() : base() { }
        public ActionCarryingException(string message) : base(message) {}
        public ActionCarryingException(string message, Exception inner) : base(message, inner) {}
    }

    //Too much work to manage the list of services for all these derived classes
    public class EntityControllerServices
    {
        public ContentDbContext context;
        public IMapper mapper;
        public PermissionService permission;
        public QueryService query;
        public ISessionService session;
        public IEmailService email;
        public ILanguageService language;
        public IHashService hash;
        public AccessService access;
        public IEntityService entity;
        public ILoggerFactory logFactory;

        public EntityControllerServices(ContentDbContext context, IMapper mapper, 
            PermissionService permissionService, QueryService queryService,
            ISessionService sessionService, IEmailService emailService, 
            ILanguageService languageService, IHashService hashService,
            AccessService accessService, IEntityService entityService,
            ILoggerFactory logFactory)
        {
            this.context = context;
            this.mapper = mapper;
            this.permission = permissionService;
            this.query = queryService;
            this.session = sessionService;
            this.email = emailService;
            this.language = languageService;
            this.hash = hashService;
            this.access = accessService;
            this.entity = entityService;
            this.logFactory = logFactory;
        }
    }

    //Keep the class abstract so it can't be used as a route? Maybe?
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public abstract class EntityController<T,V> : ControllerBase where T : EntityChild where V : EntityView
    {
        protected EntityControllerServices services;
        protected ILogger logger;

        protected bool DoActionLog = true;

        public EntityController(EntityControllerServices services)
        {
            this.services = services;
            services.session.Context = this; //EEEWWWW
            this.logger = services.logFactory.CreateLogger(GetType());
        }

        // *************
        // * UTILITIES *
        // *************
        protected void ThrowAction(ActionResult result, string message = null)
        {
            logger.LogTrace($"Throwing action {result} : {message}");

            if(message != null)
                throw new ActionCarryingException(message) {Result = result};
            else
                throw new ActionCarryingException() {Result = result};
        }

        protected async Task LogActIgnoreAnonymous(EntityAction action, long entityId)
        {
            var user = services.session.GetCurrentUid();

            if(user > 0)
                await LogAct(action, entityId, user);
            else
                logger.LogDebug($"Tried to log {action}({entityId}) for anonymous user (ignoring)");

            //This may be TOO verbose
        }

        protected async Task LogAct(EntityAction action, long entityId, long? createUserOverride = null)
        {
            logger.LogTrace($"Logging action {action}({entityId})");

            var log = new EntityLog()
            {
                action = action,
                createDate = DateTime.Now,
                entityId = entityId,
                userId = createUserOverride ?? services.session.GetCurrentUid()
            };

            if(log.userId < 0)
                throw new InvalidOperationException("Cannot log actions for anonymous users!");

            await services.context.EntityLogs.AddAsync(log);
            await services.context.SaveChangesAsync();
        }

        public async Task<UserEntity> GetCurrentUserAsync()
        {
            return await services.context.UserEntities.FindAsync(services.session.GetCurrentUid());
        }

        public async Task<bool> CanUserAsync(Permission permission)
        {
            var user = await GetCurrentUserAsync();

            if(user == null)
                return false;

            return services.permission.CanDo(user.role, permission);
        }

        public async Task CheckRequiredParentReadAsync<W>(long parentId) where W : EntityChild 
        {
            var parent = await services.query.GetSingleWithQueryAsync(services.context.Set<W>(), parentId);
            var name = typeof(W).Name;

            if(parent == null)
                ThrowAction(BadRequest($"Must provide {name} for {typeof(T).Name}!"));

            var user = await GetCurrentUserAsync();
            await services.entity.IncludeSingleAsync(parent, services.context);

            if(!services.access.CanCreate(parent.Entity, user))
                ThrowAction(Unauthorized($"You can't create {name} here!"));
        }

        // ************
        // * OVERRIDE *
        // ************

        protected virtual async Task<IQueryable<T>> GetAllBaseAsync()
        {
            logger.LogTrace("GetAllBase called");
            return services.access.WhereReadable(services.entity.IncludeSet(services.context.Set<T>()), await GetCurrentUserAsync());
        }

        protected virtual async Task<T> GetSingleBaseAsync(long id)
        {
            logger.LogTrace($"GetSingleBase called for {id}");
            return await services.query.GetSingleWithQueryAsync(await GetAllBaseAsync(), id);
            //var results = //await QueryAsync(new CollectionQuery() { ids = id.ToString() });
            //return await results.FirstAsync();
        }

        protected virtual Task GetSingle_PreResultCheckAsync(T item) { return Task.CompletedTask; }

        protected virtual Task<T> Post_ConvertItemAsync(V item) 
        { 
            logger.LogTrace($"Post_ConvertItemAsync called with {item}");

            try
            {
                var entity = services.entity.ConvertFromView<T,V>(item);
                entity.Entity.userId = services.session.GetCurrentUid();
                return Task.FromResult(entity);
            }
            catch(InvalidOperationException ex)
            {
                ThrowAction(BadRequest(ex.Message));
                throw ex; //compiler plz (this is actually thrown above)
            }
        }

        protected virtual Task Put_ConvertItemAsync(V item, T existing) 
        { 
            logger.LogTrace($"Put_ConvertItemAsync called with {item}");

            try
            {
                services.entity.FillExistingFromView(item, existing);
                return Task.CompletedTask;
            }
            catch(InvalidOperationException ex)
            {
                ThrowAction(BadRequest(ex.Message));
                throw ex; //compiler plz (this is actually thrown above)
            }
        }

        protected virtual Task Delete_PrecheckAsync(T item)
        {
            return Task.CompletedTask;
        }

        //public async virtual Task<IQueryable<T>> QueryAsync(CollectionQuery query)
        //{
        //    //Do stuff in between these (maybe?) in the future or... something.
        //    IQueryable<T> baseResults = await GetAllBase();
        //    IQueryable<T> queryResults = null;

        //    try
        //    {
        //        queryResults = services.query.ApplyQuery(baseResults, query);
        //    }
        //    catch (InvalidOperationException ex)
        //    {
        //        ThrowAction(BadRequest(ex.Message));
        //    }

        //    return queryResults;
        //}

        [HttpGet]
        [AllowAnonymous]
        public async virtual Task<ActionResult<Dictionary<string, object>>> Get([FromQuery]CollectionQuery query)
        {
            logger.LogDebug($"Get called for {query}");

            try
            {
                var result = services.query.ApplyQuery(await GetAllBaseAsync(), query);
                var views = (await result.ToListAsync()).Select(x => services.entity.ConvertFromEntity<T, V>(x));
                return services.query.GetGenericCollectionResult(views);
            }
            catch(ActionCarryingException ex)
            {
                return ex.Result;
            }
        }

        [HttpGet("{id}")]
        [AllowAnonymous]
        public async virtual Task<ActionResult<V>> GetSingle(long id)
        {
            logger.LogDebug($"GetSingle called for {id}");

            try
            {
                var item = await GetSingleBaseAsync(id); 
                await GetSingle_PreResultCheckAsync(item);
                await LogActIgnoreAnonymous(EntityAction.View, item.Entity.id);
                return services.entity.ConvertFromEntity<T, V>(item);
            }
            catch(ActionCarryingException ex)
            {
                return ex.Result;
            }
            catch(Exception)
            {
                return NotFound();
            }
        }

        [HttpPost]
        public async virtual Task<ActionResult<V>> Post([FromBody]V item)
        {
            logger.LogDebug($"Post called for {item}");

            try
            {
                //Convert the user-provided object into a real one. Controllers can perform 
                //checks and whatever on objects during the conversion.
                var newThing = await Post_ConvertItemAsync(item);

                //Actually add the object??
                await services.context.Set<T>().AddAsync(newThing);
                await services.context.SaveChangesAsync();

                await LogAct(EntityAction.Create, newThing.entityId);

                return services.entity.ConvertFromEntity<T,V>(newThing);
            }
            catch(ActionCarryingException ex)
            {
                return ex.Result;
            }
        }

        //Note: I don't think you need "Patch" because the way the "put" conversion works just... works.
        [HttpPut("{id}")]
        public async virtual Task<ActionResult<V>> Put([FromRoute]long id, [FromBody]V item)
        {
            logger.LogDebug($"Put called for {id}, {item}");

            try
            {
                var existing = await GetSingleBaseAsync(id);

                if(!services.access.CanUpdate(existing.Entity, await GetCurrentUserAsync()))
                    return Unauthorized("You cannot update this item!");

                //Now actually "convert" the item by placing it "into" the existing (assume existing gets modified in-place?)
                await Put_ConvertItemAsync(item, existing);

                //Actually update the object now? I hope???
                services.context.Set<T>().Update(existing);
                await services.context.SaveChangesAsync();

                await LogAct(EntityAction.Update, existing.entityId);

                return services.mapper.Map<V>(existing);
            }
            catch(ActionCarryingException ex)
            {
                return ex.Result;
            }
        }

        [HttpDelete("{id}")]
        public async virtual Task<ActionResult<V>> Delete([FromRoute]long id)
        {
            logger.LogDebug($"Delete called for {id}");

            try
            {
                var existing = await GetSingleBaseAsync(id);

                if(!services.access.CanDelete(existing.Entity, await GetCurrentUserAsync()))
                    return Unauthorized("You cannot delete this item!");

                await Delete_PrecheckAsync(existing);

                existing.Entity.status |= EntityStatus.Deleted;
                services.context.Set<T>().Update(existing);
                await services.context.SaveChangesAsync();
                await LogAct(EntityAction.Delete, existing.entityId);

                return services.mapper.Map<V>(existing);
            }
            catch(ActionCarryingException ex)
            {
                return ex.Result;
            }
        }
    }
}