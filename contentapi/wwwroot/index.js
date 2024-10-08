// haloopdy - 01/2022
// The script for the ultra-simple frontend implementation meant to serve as a simple example
// for other frontend designers on how to consume the API.

var api;

const SUBPAGESPERPAGE = 100;
const COMMENTSPERPAGE = 100;
const SEARCHRESULTSPERPAGE = 100;
const TINYAVATAR = 30;

const LOGINEXPIRELONG = 60 * 60 * 24 * 365;

const BLOGHOSTS = {
    "qcs.shsbs.xyz" : "https://qcs.shsbs.xyz/share",
    "oboy.smilebasicsource.com" : "https://oboy.smilebasicsource.com/share"
};

//NOTE: although this is set up to use templates and dynamic loading as an example, this is NOT
//SPA. It does not attempt to intercept URLS and do all the fanciness required for that.
window.onload = function()
{
    //NOTE: there is no error checking in this frontend, because it is not meant to be used full time. Furthermore,
    //I think it would distract from the ideas I'm trying to convey, which is just how the API is used to make
    //a functioning website.

    var parameters = new URLSearchParams(location.search);
    var state = Object.fromEntries(parameters);
    api = new Api(null, GetToken); //Just a global api object, whatever. Null means use the default endpoint (local to self)

    //Unhide the blogs if we're in the right host for that
    if(Object.keys(BLOGHOSTS).includes(location.host))
        document.getElementById("blogs-navlink").removeAttribute("hidden");

    //Load a template! Otherwise, just leave the page as-is
    if(parameters.has("t"))
    {
        var t = parameters.get("t");
        LoadPage(t, state);
        (document.querySelector(`header a[href*="${t}"]`) || {}).className += " active";
    }
    else
    {
        document.querySelector('header a[href="?"]').className += " active";
    }
};

// -- Getters and setters for stateful (cross page load) stuff --

const TOKENKEY = "contentapi_defimpl_userkey";
function GetToken() { return localStorage.getItem(TOKENKEY); }
function SetToken(token) { localStorage.setItem(TOKENKEY, token); }


// -- General utilities (for this page) --

//Convert "page state" to a url. This frontend is very basic!
function StateToUrl(state)
{
    var params = new URLSearchParams();

    for(var k in state)
    {
        if(state.hasOwnProperty(k) && state[k])
            params.set(k, state[k]);
    }

    return "?" + params.toString();
}

//Make a "deep copy" of the given object (sort of)
function Copy(object) { return JSON.parse(JSON.stringify(object)); }

// For simplicity, convert an object into something editable in a single simple textbox
function QuickObjectToInput(object)
{
    var valueStr = JSON.stringify(object);
    return valueStr.substring(1, valueStr.length - 1);
}

// For simplicity, the opposite of above (take a single simple textbox and convert to object)
function QuickInputToObject(value)
{
    if(value) return JSON.parse("{" + value + "}");
    else return {};
}

function AboutToOptions(codes, container)
{
    Object.keys(codes).forEach(k =>
    {
        var option = document.createElement("option");
        option.textContent = codes[k];
        option.value = k;
        container.appendChild(option);
    });
}

function activity_loadinto(container, result, restorable)
{
    api.AutoLinkContent(result.objects.activity, result.objects.content);
    api.AutoLinkUsers(result.objects.activity, result.objects.user);
    console.log(result.objects);
    result.objects.activity.forEach(x =>
    {
        if(restorable && x.content && x.id != x.content.lastRevisionId)
            x.restorable = true;

        var item = LoadTemplate(`activity_item`, x);
        container.appendChild(item);
    });
}

function ObjectToDisplay(obj)
{
    return JSON.stringify(obj).replaceAll("\",\"", "\", \"");
}

// Essentially a spoiler maker, useful for collapsible elements. The button is the control for showing or hiding
// the cointainer, and the visibleState is the initial visibility (set to false to hide initially). Note: the
// button's text content is overwritten for this.
function SetCollapseButton(button, container, visibleState)
{
    var toggle = function(forceVisibleState)
    {
        var vstate = forceVisibleState !== undefined ? forceVisibleState : container.hasAttribute("hidden"); //className.indexOf("hidden") >= 0;

        if(vstate) //If it's supposed to be visible, this
        {
            if(container.hasAttribute("hidden"))
                container.removeAttribute("hidden");
            button.textContent = "-";
        }
        else //If it's supposed to be hidden (not visible), this
        {
            container.setAttribute("hidden", "")
            button.textContent = "+";
        }
    };

    button.onclick = function() { toggle() };

    toggle(visibleState);
}


// -- Some basic templating functions --

// Our templates are all stored in a particular place, this loads them from that place
// by default. id is assumed to be the literal id for the template within the 
// template container.
function LoadTemplate(id, state, templates)
{
    templates = templates || document.getElementById("templates").content;
    var baseTemplate = templates.getElementById(id);
    var template = baseTemplate.cloneNode(true);
    template.id = ""; //remove the id placed in the template

    //If a cloning function function is found, run it. The cloning function will probably set up
    //specific page values or whatever. 
    if(template.hasAttribute("data-onload"))
        window[template.getAttribute("data-onload")](template, state);
    
    return template;
}

// Our pages are all loaded in a standard fashion, this does all the work of loading a template
// into the given destination (by default, the main page area)
function LoadPage(id, state, destination)
{
    destination = destination || document.getElementById("main");
    var template = LoadTemplate(`t_${id}`, state);
    destination.innerHTML = "";
    destination.appendChild(template);
}

// Display the given data inside the given table element
function MakeTable(data, table)
{
    for(var p in data)
    {
        var row = document.createElement("tr");
        var name = document.createElement("td");
        var value = document.createElement("td");
        name.textContent = p;
        value.textContent = data[p];
        row.appendChild(name);
        row.append(value);
        table.appendChild(row);
    }
}

// Using the given list, fill the given datalist element
function FillOptions(list, datalist)
{
    datalist.innerHTML = "";
    list.forEach(x =>
    {
        var option = document.createElement("option");
        option.textContent = x;
        datalist.appendChild(option);
    });
}

//Place a url into the given link with the given state modified. Does NOT modify the 
//actual state given, that's the point of this function!
function LinkState(link, state, modify)
{
    var newState = Copy(state);
    modify(newState);
    link.href = StateToUrl(newState);
}

//Set up pagination, with the given page up and page down elements
function SetupPagination(linkUp, linkDown, state, field, hash)
{
    //Fix state immediately, it's fine.
    state[field] = Math.max(0, state[field] || 0);

    if(state[field] > 0)
        LinkState(linkDown, state, x => x[field]--);

    LinkState(linkUp, state, x => x[field]++);

    if(hash)
    {
        linkDown.href = linkDown.href.replace("#", "") + "#" + hash;
        linkUp.href = linkUp.href.replace("#", "") + "#" + hash;
    }
}


// -----------------------------------------
// -- The individual page setup functions --
// -----------------------------------------

function confirmregister_onload(template, state)
{
    //It's not in the page yet, so need to use queryselector
    var userEmail = template.querySelector("#confirmregister-email");
    userEmail.value = state.email;
}

function user_onload(template, state)
{
    SetupPagination(template.querySelector("#user-files-pageup"), template.querySelector("#user-files-pagedown"), state, "fp");
    SetCollapseButton(template.querySelector("#user-update-toggle"), template.querySelector("#user-update-form"), false);
    SetCollapseButton(template.querySelector("#user-file-upload-toggle"), template.querySelector("#user-file-upload"), false);

    var table = template.querySelector("#user-table");
    var avatar = template.querySelector("#user-avatar");
    var userFiles = template.querySelector("#user-files-container");

    //This gets the information for the current user, if there is one (just the user, not anything else)
    api.UserSelf(new ApiHandler(d =>
    {
        //Set up the basic user information on the page, but we need to go out and get more
        var user = d.result;
        avatar.src = api.GetFileUrl(user.avatar, new FileModifyParameter(50));
        MakeTable(user, table);

        var userEditor = LoadTemplate("user_editor", user);
        template.querySelector("#user-update-form").appendChild(userEditor);

        //This search is to get files and such
        var search = new RequestParameter({
            uid : user.id,
            type : 3
        }, [
            new RequestSearchParameter("content", "*", "createUserId = @uid and contentType = @type", "id_desc", SEARCHRESULTSPERPAGE, state.fp * SEARCHRESULTSPERPAGE),
        ]);

        api.Search(search, new ApiHandler(dd =>
        {
            //So the data from the api is in "result", but results from the "request"
            //endpoint are complicated and contain additional information about the request,
            //so you have to look into "data", and because request can get ANY data from the 
            //database you want, you have to go into "content" because that's what you asked for.

            //Now go set up the file list display thing
            dd.result.objects.content.forEach(x => {
                var item = LoadTemplate(`file_item`, x);
                userFiles.appendChild(item);
            });
        }));
    }));
}

function page_onload(template, state)
{
    state.pid = Number(state.pid || 0);

    SetupPagination(template.querySelector("#page-subpageup"), template.querySelector("#page-subpagedown"), state, "sp");
    SetupPagination(template.querySelector("#page-commentup"), template.querySelector("#page-commentdown"), state, "cp");
    SetupPagination(template.querySelector("#history-older"), template.querySelector("#history-newer"), state, "hp");

    template.querySelector("#page-interactions").setAttribute("data-pageid", state.pid);
    template.querySelector("#page-chat-link").setAttribute("href", "chat.html?pid=" + state.pid);

    var table = template.querySelector("#page-table");
    var content = template.querySelector("#page-content");
    var title = template.querySelector("#page-title");
    var subpagesElement = template.querySelector("#page-subpages");
    var commentsElement = template.querySelector("#page-comments");
    var voteOptionsElement = template.querySelector("#vote-options-page");
    var historyList = template.querySelector("#page-history-list");
    var voteDeleteElement = template.querySelector("#vote-delete-page");

    APICONST.VOTES.forEach(k =>
    {
        var option = document.createElement("option");
        option.textContent = k;
        option.value = k;
        voteOptionsElement.appendChild(option);
    });

    template.querySelector("#vote-page").onclick = () => {
        api.SetPageEngagement(state.pid, "vote", voteOptionsElement.value, new ApiHandler(_ => { location.reload(); }));
    };

    voteDeleteElement.onclick = () => {
        api.DeletePageEngagement(state.pid, "vote", new ApiHandler(_ => { location.reload(); }));
    };

    SetCollapseButton(template.querySelector(`#page-history-toggle`), template.querySelector("#page-history-container"), state.hp);

    var setupEditor = function(type, page)
    {
        //Need to load the page editor! Since it's a "new" editor, the pid is 0
        var pageEditor = LoadTemplate("page_editor", page);
        var editorContainer = template.querySelector(`#page-${type}-container`);
        SetCollapseButton(template.querySelector(`#page-${type}-toggle`), editorContainer, false);
        editorContainer.appendChild(pageEditor);
    };

    setupEditor("submit", {parentId : state.pid, id : 0});

    //Setup the comment submit stuff if we're not at root, otherwise hide comment submit
    if(state.pid)
    {
        api.FillMarkupSelect(template.querySelector("#comment-submit-markup"));
        template.querySelector("#comment-submit-contentid").value = state.pid;
    }
    else
    {
        template.querySelector("#comment-submit-container").setAttribute("hidden", ""); 
        template.querySelector("#page-edit-section").setAttribute("hidden", ""); 
    }

    api.Search_BasicPageDisplay(state.pid, SUBPAGESPERPAGE, state.sp, COMMENTSPERPAGE, state.cp, new ApiHandler(d =>
    {
        if(d.result.objects.content.length == 0)
        {
            if(state.pid == 0)
                title.textContent = "Root parent (not a page)";
            else
                title.textContent = "Unknown page / root";
        }
        else
        {
            api.Activity(state.pid, SEARCHRESULTSPERPAGE, state.hp, new ApiHandler(dd =>
            {
                activity_loadinto(historyList, dd.result, true);
            }));
            var page = d.result.objects.content[0];
            var parent = d.result.objects.parent[0];
            var originalPage = JSON.parse(JSON.stringify(page));
            title.textContent = page.name;
            Markup.convert_lang(page.text, page.values.markupLang || "plaintext", content);
            //content.appendChild(Parse.parseLang(page.text, page.values.markupLang || "plaintext"));
            delete page.name;
            delete page.text;
            page.engagement = ObjectToDisplay(page.engagement);
            page.values = ObjectToDisplay(page.values);
            page.keywords = ObjectToDisplay(page.keywords);
            page.permissions = ObjectToDisplay(page.permissions);
            MakeTable(page, table);

            setupEditor("edit", originalPage);
            content.removeAttribute('hidden');
            template.querySelector("#page-chat-link").removeAttribute("hidden");
            template.querySelector("#page-raw-link").removeAttribute("hidden");
            template.querySelector("#page-history-section").removeAttribute("hidden");
            template.querySelector("#page-delete").removeAttribute("hidden");
            template.querySelector("#vote-submit-page").removeAttribute("hidden");
            template.querySelector("#page-raw-link").setAttribute("href", api.GetRawContentUrl(page.hash));

            if(page.contentType == 3) //A file
            {
                var filelink = api.GetFileUrl(page.hash);
                var filepagelink = template.querySelector("#filepage-link");
                filepagelink.removeAttribute("hidden");
                filepagelink.href = filelink;
                template.querySelector("#filepage-image").src = filelink;
            }

            var parentLink = template.querySelector("#page-parent-link");
            parentLink.removeAttribute("hidden");
            parentLink.href = "?t=page&pid=" + page.parentId;
            parentLink.textContent = "Parent: " + (parent ? parent.name : page.parentId == 0 ? "Root" : "???");

            //Display watch/unwatch based on if they're watching
            if(d.result.objects.watch.length)
                template.querySelector("#unwatch-page").removeAttribute("hidden");
            else
                template.querySelector("#watch-page").removeAttribute("hidden");
            
            var myVotes = d.result.objects.content_engagement.filter(x => x.type === "vote");

            if(myVotes.length) //d.result.objects.content_engagement.length)
            {
                voteDeleteElement.removeAttribute("hidden");
                template.querySelector("#current-vote-container-page").removeAttribute("hidden");
                template.querySelector("#current-vote-page").textContent = myVotes[0].engagement; //voteCodes[d.result.objects.vote[0].vote];
            }
        }

        //Waste a few cycles linking some stuff together!
        api.AutoLinkUsers(d.result.objects.subpages, d.result.objects.user);
        api.AutoLinkUsers(d.result.objects.message, d.result.objects.user);

        if(d.result.objects.subpages.length === 0)
        {
            template.querySelector("#page-subpages-container").setAttribute("hidden", "");
        }
        else
        {
            d.result.objects.subpages.forEach(x => {
                var template = x.contentType === 3 ? "file_item" : "page_item";
                var subpage = LoadTemplate(template, x);
                subpagesElement.appendChild(subpage);
            });
        }

        d.result.objects.message.forEach(x => {
            var comment = LoadTemplate("comment_item", x);
            commentsElement.appendChild(comment);
        });

        if(!d.result.requestUser)
            [...template.querySelectorAll("[data-requireslogin]")].forEach(x => x.setAttribute("hidden", ""));
    }));
}

function blogs_onload(template, state)
{
    var search = new RequestParameter({
        "key" : "share",
        "value" : "true",
        "type" : 1 //Remember, 1 is standard "page" type (not a file or module)
    }, [
        new RequestSearchParameter("content", APICONST.FIELDSETS.CONTENTQUICK, "!valuelike(@key, @value) and contentType = @type", "id", undefined, undefined, "main"),
        new RequestSearchParameter("user", "*", "id in @main.createUserId", ""),
    ]);

    var resultList = template.querySelector("#published-blogs")
    var blogsLink = BLOGHOSTS[location.host];

    if(!blogsLink)
    {
        resultList.innerHTML = "This doesn't appear to be a blog-enabled host!";
        return;
    }

    api.Search(search, new ApiHandler(d =>
    {
        //If we DID ask for the users, link them
        if(d.result.objects.user)
            api.AutoLinkUsers(d.result.objects.main, d.result.objects.user);

        //Clear whatever was in there
        resultList.innerHTML = "";

        d.result.objects.main.forEach(x => {
            x.blogsLink = blogsLink;
            var item = LoadTemplate("page_item", x);
            resultList.appendChild(item);
        });

        //aboutElement.textContent = `Search took ${d.result.totalTime} ms`;
    }));
}

function search_onload(template, state)
{
    SetupPagination(template.querySelector("#search-up"), template.querySelector("#search-down"), state, "sp");

    //Do some initial set up. Fill in some of the fields and such
    var searchtext = template.querySelector("#search-text")
    var searchtype = template.querySelector("#search-type")
    var searchfield = template.querySelector("#search-field");
    var searchsort = template.querySelector("#search-sort");

    var resultElement = template.querySelector("#search-results");
    var aboutElement = template.querySelector("#search-about");

    searchtext.value = state.search || "";
    searchtype.value = state.type || "page";

    //Also, go out and get the "about" information so we can fill in the datalist elements for options
    api.AboutSearch(new ApiHandler(d => 
    {
        //Assume the format is known, and the data will be fine.
        var resetOptions = function()
        {
            var searchType = searchtype.value;
            if(searchType === "page" || searchType === "file") searchType = "content";
            if(searchType === "comment") searchType = "message";
            console.log(searchType, d);
            var typeInfo = d.result.details.types[searchType];
            FillOptions(api.GetQueryableFields(typeInfo), searchfield);
            var sortOptions = [];
            api.GetRequestableFields(typeInfo).forEach(x =>
            {
                sortOptions.push(x);
                sortOptions.push(x + "_desc");
            });
            FillOptions(sortOptions, searchsort);
        };
        searchtype.oninput = resetOptions;
        resetOptions();

        //Now that the options have been reset based on the search criteria, reset the junk
        searchfield.value = state.field || "";
        searchsort.value = state.sort || "";
    }));

    //If a type was at least given, we can perform a search (probably). remember, this onload function is both for loading
    //the search page state AND performing the search, since oldschool forms just submit the data to the same page.
    if(state.type)
    {
        //This is how you'd set up your own search
        var values = {};
        var query = [];

        if(state.search)
        {
            values.search = `%${state.search}%`;
            query.push(`${state.field} LIKE @search`);
        }

        var searchType = state.type;

        //We have ONE type now, and that makes this auto search a bit more complex
        if(searchType == "file")
        {
            //Goddamn make this a macro, shit
            values.filetype = 3;
            searchType = "content";
            query.push("contentType = @filetype");
        } 
        else if(searchType == "page")
        {
            values.pagetype = 1;
            searchType = "content";
            query.push("contentType = @pagetype");
        } 
        else if(searchType == "comment")
        {
            searchType = "message";
            query.push("!null(module) and !notdeleted()");
        }

        var requests = [
            new RequestSearchParameter(searchType, "*", query.join(" AND "), state.sort, SEARCHRESULTSPERPAGE, state.sp * SEARCHRESULTSPERPAGE, "main"),
        ];

        //Can't link users to themselves... not really
        if(state.type != "user")
            requests.push(new RequestSearchParameter("user", "*", "id in @main.createUserId", ""));

        var search = new RequestParameter(values, requests);

        api.Search(search, new ApiHandler(d =>
        {
            //If we DID ask for the users, link them
            if(d.result.objects.user)
                api.AutoLinkUsers(d.result.objects.main, d.result.objects.user);

            d.result.objects.main.forEach(x => {
                var item = LoadTemplate(`${state.type}_item`, x);
                resultElement.appendChild(item);
            });

            aboutElement.textContent = `Search took ${d.result.totalTime} ms`;
        }));
    }
}

function notifications_onload(template, state)
{
    var container = template.querySelector("#notifications-container");
    SetupPagination(template.querySelector("#notifications-up"), template.querySelector("#notifications-down"), state, "np");

    api.Notifications(SEARCHRESULTSPERPAGE, state.np, new ApiHandler(d => {
        api.AutoLinkContent(d.result.objects.watch, d.result.objects.content);
        console.log(d.result.objects);
        d.result.objects.watch.forEach(x => {
            var item = LoadTemplate(`notification_item`, x);
            container.appendChild(item);
        });
    }));
}

function uservariables_onload(template, state)
{
    var container = template.querySelector("#uservariables-container");
    SetupPagination(template.querySelector("#uservariables-up"), template.querySelector("#uservariables-down"), state, "uvp");

    api.Search_AllByType("uservariable", "key,value,userId", "key", SEARCHRESULTSPERPAGE, state.uvp, new ApiHandler(d =>
    {
        console.log(d.result.objects);
        d.result.objects.uservariable.forEach(x =>
        {
            var item = LoadTemplate(`uservariable_item`, x);
            container.appendChild(item);
        });
    }));
}

function admin_onload(template, state)
{
    SetupPagination(template.querySelector("#adminlog-up"), template.querySelector("#adminlog-down"), state, "alp");
    SetupPagination(template.querySelector("#ban-up"), template.querySelector("#ban-down"), state, "bp", "ban-title");

    var activeOnlyInput = template.querySelector("#ban-activeonly");
    activeOnlyInput.checked = state.abonly;
    activeOnlyInput.oninput = function()
    {
        state.abonly = activeOnlyInput.checked;
        location.href = StateToUrl(state) + "#ban-title";
    };

    var adminLogTypeElement = template.querySelector("#filter-logtype")
    var adminFilterSubmit = template.querySelector("#filter-submit")

    api.GetRegistrationConfig(new ApiHandler(dd =>
    {
        var registrationInput = template.querySelector("#admin-registrationenabled");
        registrationInput.checked = dd.result.enabled;
        registrationInput.disabled = false;
    }));

    api.AboutSearch(new ApiHandler(dd =>
    {
        AboutToOptions(dd.result.details.codes.AdminLogType, adminLogTypeElement);
        if(state.logtype) adminLogTypeElement.value = state.logtype;
    }));

    adminFilterSubmit.onclick = function()
    {
        state.logtype = adminLogTypeElement.value;
        location.href = StateToUrl(state);
    };

    var logValues = { logtype : state.logtype };
    var logSearch = new RequestSearchParameter("adminlog", "*", "", "id_desc", SEARCHRESULTSPERPAGE, state.alp * SEARCHRESULTSPERPAGE);
    var logQueries= [];

    if(logValues.logtype)
        logQueries.push("type = @logtype");
    
    if(logQueries.length)
        logSearch.query = logQueries.join(" AND ");

    api.Search(new RequestParameter(logValues, [ logSearch ]), new ApiHandler(d =>
    {
        console.log(d.result.objects);
        var container = template.querySelector("#adminlog-container");
        d.result.objects.adminlog.forEach(x =>
        {
            var item = LoadTemplate(`adminlog_item`, x);
            container.appendChild(item);
        });
    }));

    api.Search(new RequestParameter({ }, [
        new RequestSearchParameter("ban", "*", state.abonly ? "!activebans()" : "", "id_desc", SEARCHRESULTSPERPAGE, SEARCHRESULTSPERPAGE * state.bp),
        new RequestSearchParameter("user", "*", "id in @ban.bannedUserId or id in @ban.createUserId",)
    ]), new ApiHandler(d =>
    {
        console.log(d.result.objects);
        var container = template.querySelector("#ban-container");
        api.AutoLinkUsers(d.result.objects.ban, d.result.objects.user);

        d.result.objects.ban.forEach(x =>
        {
            var item = LoadTemplate(`ban_item`, x);
            container.appendChild(item);
        });
    }));
}

function groupmanage_onload(template, state)
{
    SetupPagination(template.querySelector("#group-up"), template.querySelector("#group-down"), state, "grp");

    var groupEditor = LoadTemplate("user_editor", { type : 2 });
    template.querySelector("#newgroup-container").appendChild(groupEditor);

    //Search for groups
    api.Search(new RequestParameter({ "type" : 2 }, [
        new RequestSearchParameter("user", "*", "type = @type", "id_desc", SEARCHRESULTSPERPAGE, SEARCHRESULTSPERPAGE * state.grp)
    ]), new ApiHandler(d =>
    {
        console.log(d.result.objects);
        var container = template.querySelector("#group-list");

        d.result.objects.user.forEach(x =>
        {
            var item = LoadTemplate(`group_item`, x);
            container.appendChild(item);
        });
    }));
}

function activity_onload(template, state)
{
    SetupPagination(template.querySelector("#activity-older"), template.querySelector("#activity-newer"), state, "actp");

    var container = template.querySelector("#activity-list");
    api.Activity(0, SEARCHRESULTSPERPAGE, state.actp, new ApiHandler(d =>
    {
        activity_loadinto(container, d.result);
    }));
}

// This function is an excellent example for auto websocket usage. This is the
// onload function for the websocket tester page, and utilizes the basic features
// of the auto websocket.
function websocket_onload(template, state)
{
    var connectButton = template.querySelector("#websocket_connect");
    var closeButton = template.querySelector("#websocket_close");
    var sendButton = template.querySelector("#websocket_send");
    var output = template.querySelector("#websocket_output");
    var type = template.querySelector("#websocket_type");
    var data = template.querySelector("#websocket_data");
    var ws = false;
    var wslog = function(message, className) {
        var div = document.createElement("div");
        div.textContent = message;
        if (className) div.className = className;
        output.appendChild(div);
        output.scrollTop = output.scrollHeight;
    };
    connectButton.onclick = function()
    {
        if(ws)
        {
            wslog("Websocket already open, close it first!");
            return;
        }

        //To create an auto-managed websocket connection, simply call the function on the api. 
        //It will do all the setup necessary and return an instantly usable websocket. You don't
        //even have to wait for it to open, you can immediately start calling sendRequest. It is
        //a standard javascript WebSocket object, but with additional functions added to it. You
        //CAN use the standard "send" function, but if you're going for a manual approach, I would
        //suggest against AutoWebsocket. Use GetRawWebsocket instead. The websocket it returns will
        //auto-reconnect on any critical error. If you call .close(), it will no longer reconnect,
        //and the websocket becomes unusable (like a normal javascript WebSocket object). 
        ws = api.AutoWebsocket(new WebsocketAutoConfig(
            live => {
                wslog("Live data: \n" + JSON.stringify(live.data, null, 2), "systemmsg");
            }, 
            userlist => {
                wslog("Userlist data : \n" + JSON.stringify(userlist.data, null, 2), "userlistmsg");
            },
            //The websocket state itself, as well as reconnects, are handled automatically. However,
            //if you want to keep track of the errors going on so you can do your OWN things (not impacting
            //the auto websocket, since it's automatic), you use this event tracker. It reports when a new
            //websocket is created (check newWs for truthy value), and also when the error was severe enough
            //to not attempt a reconnect (check closed for truthy value)
            (message, response, newWs, closed) =>
            {
                console.warn("Websocket error: ", message, response);

                if(closed)
                {
                    wslog("Websocket error forced a close, error: " + message, "error");
                    ws = null;
                }
                else if(newWs)
                {
                    console.debug("New websocket was created, tracking");
                    ws = newWs;
                }
            },
            broadcast => {
                wslog("Broadcast from client: \n" + JSON.stringify(broadcast.data, null, 2), "broadcastmsg")
            }
        ));

        wslog("Websocket connected! Maybe...");
    };
    closeButton.onclick = function()
    {
        if(!ws)
        {
            wslog("No websocket to close!");
            return;
        }

        ws.close();
        wslog("Manually closed websocket, should not auto-reconnect anymore");
        ws = null;
    };
    sendButton.onclick = function()
    {
        if(!ws)
        {
            wslog("Websocket not open, can't send!");
            return;
        }

        var dataObject = null;

        if(data.value)
        {
            try {
                dataObject = JSON.parse(data.value);
            }
            catch (ex) {
                wslog("Couldn't parse data, needs to be json!");
                return;
            }
        }

        //When using the auto websocket, stick to sendRequest in order to make everything automatic.
        //It automatically matches up requests with responses using automatically generated random IDS
        //stamped on the messages, allowing you to set a handler per request just like regular http calls
        ws.sendRequest(type.value, dataObject, x =>
        {
            console.log("Response data: ", x);
            wslog(`RESPONSE FOR ${type.value}:\n` + JSON.stringify(x, null, 2), "successmsg");
        });
    };
}


// ----------------------------------------------------------
// -- Loaders, but not for pages, just for little templates--
// ----------------------------------------------------------

function page_item_onload(template, state)
{
    //Set up the subpage item on load
    var type = template.querySelector("[data-type]");
    var title = template.querySelector("[data-title]");
    var time = template.querySelector("[data-time]");
    var private = template.querySelector("[data-private]");
    type.textContent = state.literalType;
    title.href = "?t=page&pid=" + state.id;
    title.textContent = state.name;
    if(state.createUser) title.title = `${state.createUser.username}`;
    time.textContent = state.createDate;
    if(!api.IsPrivate(state))
        private.style.display = "none";
    if(state.blogsLink && state.hash)
    {
        var blogLinkParent = template.querySelector("[data-bloglink-container]");
        blogLinkParent.removeAttribute("hidden");
        var blogLink = template.querySelector("[data-bloglink]");
        blogLink.href = state.blogsLink + "/" + state.hash;
        blogLink.textContent = blogLink.href;
    }
}

function comment_item_onload(template, state)
{
    var avatar = template.querySelector("[data-avatar]");
    var username = template.querySelector("[data-username]");
    var comment = template.querySelector("[data-comment]");
    var time = template.querySelector("[data-time]");
    var contentid = template.querySelector("[data-contentid]");

    if(state.createUser)
    {
        avatar.src = api.GetFileUrl(state.createUser.avatar, new FileModifyParameter(TINYAVATAR, true));
        username.textContent = state.createUser.username;
    }
    else
    {
        username.textContent = "???";
    }
    username.title = state.createUserId;
    Markup.convert_lang(state.text, state.values.markupLang || "plaintext", comment);
    comment.title = state.id;
    time.textContent = state.createDate;
    contentid.textContent = `(${state.contentId})`;
}

function activity_item_onload(template, state)
{
    var avatar = template.querySelector("[data-avatar]");
    var username = template.querySelector("[data-username]");
    var action = template.querySelector("[data-action]");
    var time = template.querySelector("[data-time]");
    var pagelink = template.querySelector("[data-pagelink]");

    template.setAttribute("data-id", state.id);
    template.setAttribute("data-contentid", state.contentId);

    if(state.user)
    {
        avatar.src = api.GetFileUrl(state.user.avatar, new FileModifyParameter(TINYAVATAR, true));
        username.textContent = state.user.username;
    }
    else
    {
        username.textContent = "???";
    }
    username.title = state.userId;
    action.textContent = api.ActionToVerb(state.action);
    action.title = state.id;
    pagelink.href = `?t=page&pid=${state.contentId}`;
    if(state.content)  //This replaces the name with the hash since the name is removed but the hash can't be. 
        pagelink.textContent = state.content.deleted ? `${state.content.hash} (${state.contentId})` : state.content.name;
    else
        pagelink.textContent = `??? (${state.contentId})`;
    if(state.restorable)
        template.querySelector("[data-restore]").removeAttribute("hidden");
        
    time.textContent = state.date;
}

function user_item_onload(template, state)
{
    var avatar = template.querySelector("[data-avatar]");
    var username = template.querySelector("[data-username]");
    var time = template.querySelector("[data-time]");
    var sup = template.querySelector("[data-super]");

    avatar.src = api.GetFileUrl(state.avatar, new FileModifyParameter(TINYAVATAR, true));
    username.textContent = state.username;
    username.title = state.id;
    time.textContent = state.createDate;

    if(!state.super)
        sup.style.display = "none";
    if(state.type !== 2)
        template.querySelector("[data-group]").style.display = "none";
}

function file_item_onload(template, state)
{
    var file = template.querySelector("[data-file]");
    var filelink = template.querySelector("[data-filelink]");
    var hash = template.querySelector("[data-hash]");
    var idelem = template.querySelector("[data-id]");
    var time = template.querySelector("[data-time]");
    var private = template.querySelector("[data-private]");

    file.src = api.GetFileUrl(state.hash, new FileModifyParameter(50));
    file.title = `${state.literalType} : ${state.meta}`;
    filelink.href = api.GetFileUrl(state.hash);
    idelem.textContent = `${state.name} [${state.id}]`;
    idelem.href = `?t=page&pid=${state.id}`;
    hash.textContent = ` pubId: ${state.hash}`;
    time.textContent = state.createDate;

    if(!api.IsPrivate(state))
        private.style.display = "none";
}

function notification_item_onload(template, state)
{
    var pagedataelem = template.querySelector("[data-pagedata]");
    var commentcountelem = template.querySelector("[data-commentcount]");
    var activitycountelem = template.querySelector("[data-activitycount]");
    var clearelem = template.querySelector("[data-clear]");

    clearelem.setAttribute("data-pageid", state.contentId);

    if(state.content)
    {
        var item = LoadTemplate("page_item", state.content);
        pagedataelem.appendChild(item);
    }
    else
    {
        pagedataelem.textContent = "???";
    }

    commentcountelem.textContent = state.commentNotifications;
    activitycountelem.textContent = state.activityNotifications;
}

function uservariable_item_onload(template, state)
{
    var keyelem = template.querySelector("[data-key]");
    var valueelem = template.querySelector("[data-value]");
    var editelem = template.querySelector("[data-edit]");
    var deleteelem = template.querySelector("[data-delete]");

    keyelem.textContent = state.key;
    valueelem.value = state.value;

    editelem.onclick = function() {
        api.SetUserVariable(state.key, valueelem.value, new ApiHandler(d => { location.reload(); }));
    };

    deleteelem.onclick = function() {
        api.DeleteUserVariable(state.key, new ApiHandler(d => { location.reload(); }));
    };
}

function adminlog_item_onload(template, state)
{
    var timeelem = template.querySelector("[data-time]");
    var idelem = template.querySelector("[data-id]");
    var textelem = template.querySelector("[data-text]");

    timeelem.textContent = new Date(state.createDate).toLocaleString();
    idelem.textContent = `[${state.id}]`;
    textelem.textContent = state.text;
}

function ban_item_onload(template, state)
{
    template.querySelector("[data-time]").textContent = new Date(state.createDate).toLocaleString();
    template.querySelector("[data-banner]").textContent = state.createUser ? state.createUser.username : `???(${state.createUserId})`;
    template.querySelector("[data-bannee]").textContent = state.bannedUser ? state.bannedUser.username : `???(${state.bannedUserId})`;
    template.querySelector("[data-type]").textContent = `Type: ${state.type}`;
    template.querySelector("[data-message]").textContent = `Message: ${state.message}`;
    template.querySelector("[data-id]").textContent = `[${state.id}]`;
    template.querySelector("[data-expire]").textContent = new Date(state.expireDate).toLocaleString();
}

function group_item_onload(template, state)
{
    template.querySelector("[data-id]").textContent = state.id;
    template.querySelector("[data-createdate").textContent = new Date(state.createDate).toLocaleDateString();
    template.querySelector("[data-createuserid").textContent = state.createUserId;

    var groupEditor = LoadTemplate("user_editor", state);
    template.querySelector("[data-editcontainer]").appendChild(groupEditor);
}

function page_editor_onload(template, state)
{
    //state is going to be the page IN the format from the api itself.
    state = state || {};
    template.querySelector("#page-editor-id").value = state.id || 0;
    template.querySelector("#page-editor-parentid").value = state.parentId || 0;
    template.querySelector("#page-editor-contenttype").value = state.contentType || 1;
    template.querySelector("#page-editor-name").value = state.name || "";
    template.querySelector("#page-editor-text").value = state.text || "";
    template.querySelector("#page-editor-description").value = state.description || "";
    template.querySelector("#page-editor-hash").value = state.hash || "";
    template.querySelector("#page-editor-type").value = state.literalType || "";

    if(state.keywords)
        template.querySelector("#page-editor-keywords").value = state.keywords.join(" ");
    if(state.values)
        template.querySelector("#page-editor-values").value = QuickObjectToInput(state.values);
    if(state.permissions)
        template.querySelector("#page-editor-permissions").value = QuickObjectToInput(state.permissions);
}

function user_editor_onload(template, user)
{
    user = user || {};
    template.querySelector("[data-user-update-id]").value = user.id || 0;
    template.querySelector("[data-user-update-type]").value = user.type || 1;
    template.querySelector("[data-user-update-username]").value = user.username || "";
    template.querySelector("[data-user-update-super]").value = Number(user.super) || 0;
    var avatarinput = template.querySelector("[data-user-update-avatar]");
    var avatarpreview = template.querySelector("[data-avatarpreview]");
    var special = template.querySelector("[data-user-update-special]");
    var groups = template.querySelector("[data-user-update-usersingroup]");
    var refreshPreview = () =>
    {
        if(avatarinput.value)
        {
            avatarpreview.src = api.GetFileUrl(avatarinput.value, new FileModifyParameter(TINYAVATAR, true));
            avatarpreview.removeAttribute("hidden");
        }
        else
        {
            avatarpreview.setAttribute("hidden", "");
        }
    };
    avatarinput.onblur = refreshPreview;
    avatarinput.value = user.avatar || "0";
    special.value = user.special || "";
    groups.value = user.usersInGroup ? user.usersInGroup.join(" ") : "";
    if(user.type === 2)
    {
        //parents because label
        special.parentNode.style.display = "none";
    } 
    else
    {
        //Only available... IN groups
        groups.parentNode.style.display = "none"; 
    }
    refreshPreview();
}


// -- Functions templates use directly (mostly form submits) --

function t_login_submit(form)
{
    var username = document.getElementById("login-username").value;
    var password = document.getElementById("login-password").value;

    var loginParam = new LoginParameter(username, password);

    if(document.getElementById("login-extended").checked)
    {
        loginParam.expireSeconds = LOGINEXPIRELONG;
        console.debug(`LONG token expiration set to: ${loginParam.expireSeconds} seconds`);
    }

    api.Login(loginParam, new ApiHandler(d => {
        SetToken(d.result);
        location.href = "?t=user";
    }));

    return false;
}

function t_recover_submit(form)
{
    var email = document.getElementById("recover-email").value;
    var sentDiv = document.getElementById("recover-sent");

    sentDiv.setAttribute("hidden", "");

    api.PasswordRecovery(email, new ApiHandler(d => {
        sentDiv.removeAttribute("hidden");
    }));

    return false;
}

function t_newpassword_submit(form)
{
    var data = {};
    data.currentEmail = document.getElementById("newpassword-email").value;
    data.currentPassword = document.getElementById("newpassword-code").value;
    data.password = document.getElementById("newpassword-password").value;
    data.email = document.getElementById("newpassword-newemail").value;
    var sentDiv = document.getElementById("newpassword-success");

    sentDiv.setAttribute("hidden", "");

    api.SetPrivateData(data, new ApiHandler(d => {
        sentDiv.removeAttribute("hidden");
    }));

    return false;
}

function t_register_submit(form)
{
    var username = document.getElementById("register-username").value;
    var email = document.getElementById("register-email").value;
    var password = document.getElementById("register-password").value;

    api.RegisterAndEmail(new RegisterParameter(username, email, password), new ApiHandler(d => {
        if(d.result.registered) //Sometimes, registration is instant (depending on config)
        {
            //This is a hack, maybe only temporarily. With instant registration, the user's "special"
            //field (which is unused as of yet) actually contains the login token
            SetToken(d.result.special);
            location.href = "?t=user";
        }
        else
        {
            location.href = `?t=confirmregister&email=${email}`;
        }
    }));

    return false;
}

function t_confirmregister_submit(form)
{
    var email = document.getElementById("confirmregister-email").value;
    var key = document.getElementById("confirmregister-key").value;

    api.ConfirmRegistration(new ConfirmRegistrationParameter(email, key), new ApiHandler(d => {
        SetToken(d.result); //This endpoint returns a user token as well, like login!
        location.href = `?t=user`;
    }));

    return false;
}

function t_user_logout()
{
    SetToken(null);
    location.href = "?"; //Home page maybe?
}

function t_comment_submit_submit(form)
{
    var text = document.getElementById("comment-submit-text").value;
    var markup = document.getElementById("comment-submit-markup").value;
    var contentId = document.getElementById("comment-submit-contentid").value;

    //NOTE: if you want the avatar you used to comment with saved with the comment for posterity
    //(meaning searching for your old comment will show your original avatar when commenting and not
    // your current avatar), you can add your avatar to the metadata. Also notice that we're using
    //the coment builder to make life simpler, this is NOT required!
    api.WriteType(APICONST.WRITETYPES.MESSAGE, new CommentBuilder(text, contentId, markup), new ApiHandler(d => {
        location.reload();
    }));

    return false;
}

function t_page_editor_submit(form)
{
    //Pull the form together. These are about all the actual values you'd write for any page!
    //It just looks complicated in real frontends because the "values" array probably contains
    //things you want individual inputs for, like "what's the key" or whatever
    var page = {
        id : Number(form.querySelector("#page-editor-id").value),
        parentId : Number(form.querySelector("#page-editor-parentid").value),
        contentType : Number(form.querySelector("#page-editor-contenttype").value),
        literalType : form.querySelector("#page-editor-type").value,
        name : form.querySelector("#page-editor-name").value,
        text : form.querySelector("#page-editor-text").value,
        keywords : form.querySelector("#page-editor-keywords").value.split(" "),
        permissions : QuickInputToObject(form.querySelector("#page-editor-permissions").value),
        hash: form.querySelector("#page-editor-hash").value,
        description: form.querySelector("#page-editor-description").value,
        values : QuickInputToObject(form.querySelector("#page-editor-values").value)
    };

    api.WriteType(APICONST.WRITETYPES.CONTENT, page, new ApiHandler(d => {
        location.href = "?t=page&pid=" + d.result.id;
    }));

    return false;
}

function t_user_files_submit(form)
{
    var data = new FormData(form);

    //We set up our form to be EXACTLY the form data that is required, so just... do that.
    api.UploadFile(data, new ApiHandler(d => {
        alert("Upload successful. ID: " + d.result.id);
        console.log("Upload successful. ID: " + d.result.id);
        location.reload();
    }));

    return false;
}

function t_user_update_submit(form)
{
    //Pull the form together. These are about all the actual values you'd write for any page!
    //It just looks complicated in real frontends because the "values" array probably contains
    //things you want individual inputs for, like "what's the key" or whatever
    var user = {
        id : Number(form.querySelector("[data-user-update-id]").value),
        type : Number(form.querySelector("[data-user-update-type]").value),
        username : form.querySelector("[data-user-update-username]").value,
        avatar : form.querySelector("[data-user-update-avatar]").value,
        special : form.querySelector("[data-user-update-special]").value,
        usersInGroup : form.querySelector("[data-user-update-usersingroup]").value.split(" ").filter(x => x).map(x => Number(x)),
        super : Number(form.querySelector("[data-user-update-super]").value)
    };

    api.WriteType(APICONST.WRITETYPES.USER, user, new ApiHandler(d => {
        location.reload();
    }));

    return false;
}

function t_page_watch(button, watch)
{
    var pid = button.parentNode.getAttribute("data-pageid");

    var handler = new ApiHandler(d => { location.reload(); })

    if(watch)
        api.WatchPage(pid, handler);
    else
        api.UnwatchPage(pid, handler);
}

function t_page_delete(button)
{
    if(!confirm("Are you SURE you want to delete this page? Only super users can restore deleted pages!"))
        return;

    var pid = button.parentNode.getAttribute("data-pageid");
    api.DeleteType(APICONST.WRITETYPES.CONTENT, pid, new ApiHandler(d => { location.href = document.querySelector("#page-parent-link").href; }));
}

function t_notification_item_clear(button)
{
    var pid = button.getAttribute("data-pageid");
    api.ClearNotifications(pid, new ApiHandler(d => {
        location.reload();
    }));
}

function t_activity_item_restore(button)
{
    var revision = button.parentNode.getAttribute("data-id");
    if(confirm(`Are you sure you want to rollback this content to revision ${revision}?`))
    {
        api.RestoreContent(revision, null, new ApiHandler(d => {
            location.reload();
        }));
    }
}

function t_uservariable_submit(form)
{
    var key = form.querySelector("#uservariables-key");
    var value = form.querySelector("#uservariables-value");
    api.SetUserVariable(key.value, value.value, new ApiHandler(d => {
        location.reload();
    }));

    return false;
}

function t_ban_submit(form)
{
    var ban = new BanParameter(
        form.querySelector("#ban-banneduserid").value, 
        form.querySelector("#ban-bantype").value,
        form.querySelector("#ban-banhours").value,
        form.querySelector("#ban-banmessage").value
    );

    api.Ban(ban, new ApiHandler(d => { location.reload(); }));

    return false;
}

function t_registrationenabled_submit(form)
{
    var enabled = form.querySelector("#admin-registrationenabled").checked;
    console.log("Enabled: ", enabled);

    if(confirm("This is a temporary setting; if the API restarts, registration will be set to the default. Are you SURE you want to change user registration to " + (enabled ? "true" : "false")+ "?"))
    {
        api.SetRegistrationConfig({ enabled: enabled }, new ApiHandler(d => { location.reload(); }));
    }

    return false;
}
