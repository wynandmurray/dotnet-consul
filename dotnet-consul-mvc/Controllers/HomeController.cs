using dotnet_consul_mvc.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Consul;
using System.Text;

namespace dotnet_consul_mvc.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View();
        }


        [HttpPost("DoTask")]
        public async Task<IActionResult> DoTask([FromBody] PostObject postObject)
        {
            var consulHandler = new ConsulHandler();
            var handlerResponse = await consulHandler.DoConsulTaskAsync();



            return Ok(handlerResponse);
        }
    }


    public class ConsulHandler
    {
        public async Task<ResponseObject> DoConsulTaskAsync()
        {
            var response = new ResponseObject();
            var client = new ConsulClient();
            var cacheKey = "wynandsCacheKey";
            var cacheResult = await client.KV.Get(cacheKey);

            var currentDateTimeTicks = DateTime.UtcNow.Ticks;
            var newExpiryDateTimeTicks = DateTime.UtcNow.AddSeconds(10).Ticks;

            if (cacheResult.Response == null)
            {
                // Add the KV entry with a 20-seconf timeout.
                var sessionId = await client.Session.Create(new SessionEntry { 
                    Name = "testsession",
                    TTL = TimeSpan.FromSeconds(10),
                    LockDelay = TimeSpan.Zero,
                    Behavior = SessionBehavior.Delete
                }).ConfigureAwait(false);

                await client.KV.Acquire(new KVPair(cacheKey)
                {
                    Value = Encoding.UTF8.GetBytes(newExpiryDateTimeTicks.ToString()),
                    Session = sessionId.Response
                });

                response.Message = "Work DNE! Cache Key Not Found... Added cache key.";
            }
            else
            {
                var cachedExpiryTicks = Convert.ToInt64(Encoding.UTF8.GetString(cacheResult.Response.Value));
                var cachedExpiryDateTime = new DateTime(cachedExpiryTicks);
                var currentDateTime = new DateTime(currentDateTimeTicks);

                var responseMessage = $"Cache key found.";
                responseMessage += $"<br />Cached Expiry Date: {cachedExpiryDateTime.ToString("dd-MM-YYYY HH:mm:ss")}";
                responseMessage += $"<br />Current Date: {currentDateTime.ToString("dd-MM-YYYY HH:mm:ss")}";
                
                if (cachedExpiryTicks < currentDateTimeTicks)
                {
                    // "refresh" the token...
                    var allSession = await client.Session.List();
                    var sessions = allSession.Response.Where(s => s.Name == "testsession").ToList();

                    foreach(var s in sessions)
                    {
                        await client.Session.Destroy(s.ID);
                    }

                    // createt the new session
                    var sessionId = await client.Session.Create(new SessionEntry
                    {
                        Name = "testsession",
                        TTL = TimeSpan.FromSeconds(10),
                        LockDelay = TimeSpan.Zero,
                        Behavior = SessionBehavior.Delete
                    }).ConfigureAwait(false);

                    await client.KV.Acquire(new KVPair(cacheKey)
                    {
                        Value = Encoding.UTF8.GetBytes(newExpiryDateTimeTicks.ToString()),
                        Session = sessionId.Response
                    });

                    responseMessage += $"<br />RESULT: Expired cache key found.<br /> The following Sessions were deleted: " + string.Join("<br />", sessions.Select(s => s.ID));
                    responseMessage += $"<br />A new session was created and the action was ALLOWED!!";
                }
                else
                {
                    responseMessage += $"<br />RESULT: Rate limited... please wait ${(cachedExpiryDateTime - currentDateTime).TotalSeconds} seconds...";
                }

                response.Message = responseMessage;
            }
            
            
            
            
            return response;
        }





        //private async Task CreateConsulCacheEntry()
        //{
        //    var client = new ConsulClient();
        //    var sessionId = await client.Session.Create(new SessionEntry
        //    {
        //        Name = "testsession",
        //        TTL = TimeSpan.FromSeconds(10),
        //        LockDelay = TimeSpan.Zero,
        //        Behavior = SessionBehavior.Delete
        //    }).ConfigureAwait(false);

        //    await client.KV.Acquire(new KVPair(cacheKey)
        //    {
        //        Value = Encoding.UTF8.GetBytes(newExpiryDateTimeTicks.ToString()),
        //        Session = sessionId.Response
        //    });
        //}




    }







    public class PostObject
    {
        public string Name { get; set; }
    }

    public class ResponseObject
    {
        public string Message { get; set; }
    }


}
