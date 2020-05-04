using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using contentapi.Services;
using contentapi.Services.Implementations;
using contentapi.Views;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Randomous.EntitySystem;

namespace contentapi.Controllers
{
    public class ActivityController : BaseSimpleController
    {
        protected ActivityViewService service;

        public ActivityController(Keys keys, ILogger<BaseSimpleController> logger,
            ActivityViewService service) : base(keys, logger)
        {
            this.service = service;
        }

        [HttpGet]
        public Task<ActionResult<ActivityResultView>> GetActivityAsync([FromQuery]ActivitySearch search)
        {
            return ThrowToAction(() => service.SearchResultAsync(search, GetRequesterNoFail()));
        }
    }
}