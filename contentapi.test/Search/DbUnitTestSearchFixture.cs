using System;
using System.Collections.Generic;
using System.Linq;
using contentapi.Utilities;
using Dapper.Contrib.Extensions;
using Microsoft.Extensions.Logging;

namespace contentapi.test;

public enum UserVariations
{
    Super = 1,
    Variables = 2,
    Special = 4,
    Registered = 8
}

public enum ContentVariations
{
    AccessBySupers = 1,
    AccessByAll = 2,
    Values = 4,
    Keywords = 8,
    Comments = 16,
    Deleted = 32,
    //TypeBits1 = 64,
    //TypeBits2 = 128
}

public class DbUnitTestSearchFixture : DbUnitTestBase, IDisposable
{
    public readonly List<string> StandardKeywords = new List<string> {
        "one",
        "two",
        "three",
        "four",
        "five",
        "six",
        "seven",
        "eight"
    };

    public readonly List<string> StandardPublicTypes = new List<string> {
        "one",
        "two",
        "three",
        "four",
        "five",
        "six",
        "seven",
        "eight"
    };

    public readonly int UserCount;
    public readonly int ContentCount;
    public readonly int AdminLogCount;
    public readonly int GroupCount;

    public long SuperUid() => 1 + (int)UserVariations.Super;
    public long StandardUid() => (int)UserVariations.Super;

    public DbUnitTestSearchFixture()
    {
        logger.LogDebug($"Adding users to database {MasterConnectionString}");

        var rng = GetService<IRandomGenerator>();

        //Create tables for the in-memory database we're testing against
        //for this particular test. Note that ALL tests that use this 
        //fixture will get the SAME database!
        using(var conn = CreateNewConnection())
        {
            using(var tsx = conn.BeginTransaction())
            {
                var users = new List<Db.User>();
                var userVariables = new List<Db.UserVariable>();
                var userRelations = new List<Db.UserRelation>();
                UserCount = (int)Math.Pow(2, Enum.GetValues<UserVariations>().Count());

                for(var i = 0; i < UserCount; i++)
                {
                    var user = new Db.User() {
                        username = $"user_{i}",
                        password = $"SECRETS_{i}",
                        avatar = rng.GetAlphaSequence(5),//UserCount - i,
                        type = Db.UserType.user,
                        createDate = DateTime.Now.AddDays(i - UserCount),
                        salt = $"SALTYSECRETS_{i}"
                    };

                    user.super = (i & (int)UserVariations.Super) > 0;
                    user.special = (i & (int)UserVariations.Special) > 0 ? $"special_{i}" : null;
                    user.registrationKey = (i & (int)UserVariations.Registered) > 0 ? null : $"registration_{i}";

                    if((i & (int)UserVariations.Variables) > 0)
                    {
                        //Variable count should equal user id
                        for(var j = 0; j <= i; j++)
                        {
                            userVariables.Add(new Db.UserVariable()
                            {
                                userId = i + 1,
                                key = $"userval_{j}_{i}",
                                value = $"value_{j}",
                                createDate = DateTime.Now.AddMinutes(-i - j)
                            });
                        }
                    }

                    users.Add(user);
                }

                //Split up the users into groups evenly
                GroupCount = 2;
                for(var i = 0; i < GroupCount; i++)
                {
                    //We don't have to test all the user stuff about this, so only the important fields
                    var group = new Db.User() {
                        username = $"group_{i}",
                        type = Db.UserType.group,
                        super = (i & (int)UserVariations.Super) > 0 //This may not actually set the group to super FYI...
                    };

                    users.Add(group);

                    for(var j = 0; j < UserCount / GroupCount; j++)
                    {
                        var userGroup = new Db.UserRelation() {
                            userId = j + 1 + i * UserCount / GroupCount,
                            type = Db.UserRelationType.inGroup,
                            relatedId = UserCount + i + 1
                        };

                        userRelations.Add(userGroup);
                    }
                }

                conn.Insert(users, tsx);
                conn.Insert(userVariables, tsx);
                conn.Insert(userRelations, tsx);

                var content = new List<Db.Content>();
                var comments = new List<Db.Comment>();
                var values = new List<Db.ContentValue>();
                var keywords = new List<Db.ContentKeyword>();
                var permissions = new List<Db.ContentPermission>();
                var watchers = new List<Db.ContentWatch>();
                var votes = new List<Db.ContentVote>();
                var history = new List<Db.ContentHistory>();
                ContentCount = (int)Math.Pow(2, Enum.GetValues<ContentVariations>().Count());

                for(var i = 0; i < ContentCount; i++)
                {
                    var c = new Db.Content() {
                        name = $"content_{i}",
                        parentId = i / 4,
                        text = $"text_{i}",
                        createUserId = 1 + (i % UserCount),
                        publicType = StandardPublicTypes[i % StandardPublicTypes.Count],
                        createDate = DateTime.Now.AddDays(i - ContentCount)
                    };

                    c.deleted = (i & (int)ContentVariations.Deleted) > 0;
                    //c.internalType = (Db.InternalContentType)((i & ((int)ContentVariations.TypeBits1 | (int)ContentVariations.TypeBits2)) >> (int)ContentVariations.TypeBits1);//(i % 4);//(i & (int)ContentVariations.PageOrFile) > 0 ? Db.InternalContentType.page : Db.InternalContentType.file;
                    c.internalType = (Db.InternalContentType)(i % 4);

                    //The activity is inversely proportional to i, but only 1/16 of the whatevers.
                    //If the content is deleted, the last history inserted should be a delete
                    var historyCount = (ContentCount - i) / 16;
                    for(var j = 0; j < historyCount; j++)
                    {
                        history.Add(new Db.ContentHistory()
                        {
                            contentId = i + 1,
                            createDate = DateTime.Now.AddDays(j - i),
                            createUserId = 1 + (i % UserCount), //All same user. Hm
                            action = (j == historyCount - 1 && c.deleted) ? Db.UserAction.delete : Db.UserAction.update
                        });
                    }

                    //Always insert watches, the amount of people watching is 1/usercount of the id.
                    //Thus the last one might have all users watching it... maybe?? mm idk. 
                    //Anyway, first few usercount content have no watches
                    for(var j = 0; j < i / (ContentCount / UserCount); j++)
                    {
                        watchers.Add(new Db.ContentWatch()
                        {
                            contentId = i + 1,
                            userId = 1 + (j % UserCount),
                            createDate = DateTime.Now
                        });
                    }

                    var random = new Random(i);
                    for(var j = 0; j < i / (ContentCount / UserCount); j++)
                    {
                        votes.Add(new Db.ContentVote()
                        {
                            contentId = i + 1,
                            userId = j % UserCount,
                            createDate = DateTime.Now,
                            vote = (Db.VoteType)(1 + random.Next() % 3)
                        });
                    }

                    //Add the last group to all permissions
                    permissions.Add(new Db.ContentPermission()
                    {
                        contentId = i + 1,
                        userId = UserCount + GroupCount,
                        create = true,
                        read = true,
                        update = true,
                        delete = true
                    });

                    if((i & (int)ContentVariations.AccessBySupers) > 0)
                    {
                        permissions.Add(new Db.ContentPermission()
                        {
                            contentId = i + 1,
                            userId = 1 + ((i | (int)UserVariations.Super) % UserCount), //User is contentid with super bit flip
                            create = true,
                            read = true,
                            update = true,
                            delete = true
                        });
                    }
                    
                    if((i & (int)ContentVariations.AccessByAll) > 0)
                    {
                        permissions.Add(new Db.ContentPermission()
                        {
                            contentId = i + 1,
                            userId = 0,
                            create = true,
                            read = true,
                            update = true,
                            delete = true
                        });
                    }

                    if((i & (int)ContentVariations.Values) > 0)
                    {
                        //Value count should equal contentId, yes even if it's a crapload
                        for(var j = 0; j <= i; j++)
                        {
                            values.Add(new Db.ContentValue()
                            {
                                contentId = i + 1,
                                key = $"contentval_{j}_{i}",
                                value = $"\"value_{j}\""
                            });
                        }
                    }

                    if((i & (int)ContentVariations.Keywords) > 0)
                    {
                        //You get the keywords for 1/8 of the id
                        for(var j = 0; j <= i/8; j++)
                        {
                            keywords.Add(new Db.ContentKeyword()
                            {
                                contentId = i + 1,
                                value = StandardKeywords[j % StandardKeywords.Count]
                            });
                        }
                    }

                    if((i & (int)ContentVariations.Comments) > 0)
                    {
                        //comment count should equal (indexed) contentId, yes even if it's a crapload
                        for(var j = 0; j < i; j++)
                        {
                            comments.Add(new Db.Comment()
                            {
                                contentId = i + 1,
                                createDate = DateTime.Now.AddDays(j - i),
                                createUserId = 1 + (j % UserCount),
                                text = $"comment_{i}",
                            });
                        }
                    }

                    content.Add(c);
                }

                conn.Insert(content, tsx);
                conn.Insert(comments, tsx);
                conn.Insert(values, tsx);
                conn.Insert(keywords, tsx);
                conn.Insert(permissions, tsx);
                conn.Insert(watchers, tsx);
                conn.Insert(votes, tsx);
                conn.Insert(history, tsx);

                var adminLogs = new List<Db.AdminLog>();
                AdminLogCount = Enum.GetValues<Db.AdminLogType>().Count();

                foreach(var type in Enum.GetValues<Db.AdminLogType>())
                {
                    adminLogs.Add(new Db.AdminLog()
                    {
                        initiator = 1 + (int)type,
                        target = 2 + (int)type,
                        text = StandardPublicTypes[(int)type],
                        createDate = DateTime.Now.AddDays(-(int)type),
                        type = type,
                    });
                }

                conn.Insert(adminLogs, tsx);

                tsx.Commit();
            }
        }

        //Make sure that resets include all these changes we just made!
        SetBackupNow();
    }
}