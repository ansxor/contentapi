using contentapi.Main;
using contentapi.Search;
using contentapi.Utilities;
using contentapi.data.Views;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using contentapi.data;

namespace contentapi.Controllers;


[Authorize()]
public class WriteController : BaseController
{
    protected IDbWriter writer;
    protected IGenericSearch searcher;

    public WriteController(BaseControllerServices services, IGenericSearch search, IDbWriter writer) : base(services)
    {
        this.searcher = search;
        this.writer = writer;
    }

    [HttpPost("message")]
    public Task<ActionResult<MessageView>> WriteMessageAsync([FromBody]MessageView message)
    {
        return MatchExceptions(async () => 
        {
            RateLimit(RateWrite);
            if(message.module != null)
                throw new ForbiddenException("You cannot create module messages yourself!");

            if(message.receiveUserId != 0)
                throw new ForbiddenException("Setting receiveUserId in a comment is not supported right now!");

            return await writer.WriteAsync(message, GetUserIdStrict());
        }); //message used for activity and such
    }

    [HttpPost("content")]
    public Task<ActionResult<ContentView>> WriteContentAsync([FromBody]ContentView page, [FromQuery]string? activityMessage) 
    {
        return MatchExceptions(async () => 
        {
            RateLimit(RateWrite);
            //THIS IS AWFUL! WHAT TO DO ABOUT THIS??? Or is it fine: files ARE written by the controllers after all...
            //so maybe it makes sense for the controllers to control this aspect as well
            if(page.id == 0 && page.contentType == InternalContentType.file)
                throw new ForbiddenException("You cannot create files through this endpoint! Use the file controller!");

            return await writer.WriteAsync(page, GetUserIdStrict(), activityMessage);
        }); //message used for activity and such
    }

    //This is SLIGHTLY special in that user writes are mostly for self updates... but might be used for new groups as well? You also
    //can't update PRIVATE data through this endpoint
    [HttpPost("user")]
    public Task<ActionResult<UserView>> WriteUserAsync([FromBody]UserView user)
    {
        return MatchExceptions(async () => 
        {
            RateLimit(RateWrite);
            return await writer.WriteAsync(user, GetUserIdStrict()); //message used for activity and such
        });
    }

    [HttpPost("watch")]
    public Task<ActionResult<WatchView>> WriteWatchAsync([FromBody]WatchView watch)
    {
        return MatchExceptions(async () => 
        {
            RateLimit(RateInteract); //A different rate for watches
            return await writer.WriteAsync(watch, GetUserIdStrict()); //message used for activity and such
        });
    }

    [HttpPost("vote")]
    public Task<ActionResult<VoteView>> WriteVoteAsync([FromBody]VoteView vote)
    {
        return MatchExceptions(async () => 
        {
            RateLimit(RateInteract); //A different rate for watches
            return await writer.WriteAsync(vote, GetUserIdStrict()); //message used for activity and such
        });
    }

    [HttpPost("uservariable")]
    public Task<ActionResult<UserVariableView>> WriteUserVariableAsync([FromBody]UserVariableView variable)
    {
        return MatchExceptions(async () => 
        {
            RateLimit(RateUserVariable);
            return await writer.WriteAsync(variable, GetUserIdStrict()); //message used for activity and such
        });
    }

    [HttpPost("ban")]
    public Task<ActionResult<BanView>> WriteBanVariableAsync([FromBody]BanView ban)
    {
        return MatchExceptions(async () => 
        {
            //RateLimit(RateUserVariable);
            return await writer.WriteAsync(ban, GetUserIdStrict()); //message used for activity and such
        });
    }
}