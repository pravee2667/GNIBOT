// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Net.Http.Headers;
using System.IO;
using System.Linq;
using System;
using System.Text.RegularExpressions;
using Microsoft.Bot.Connector;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Text;
using System.Data;
using Newtonsoft.Json.Linq;
using System.Data.SqlClient;

namespace Microsoft.BotBuilderSamples.Bots
{
    // This IBot implementation can run any type of Dialog. The use of type parameterization is to allows multiple different bots
    // to be run at different endpoints within the same project. This can be achieved by defining distinct Controller types
    // each with dependency on distinct IBot types, this way ASP Dependency Injection can glue everything together without ambiguity.
    // The ConversationState is used by the Dialog system. The UserState isn't, however, it might have been used in a Dialog implementation,
    // and the requirement is that all BotState objects are saved at the end of a turn.
    public class DialogBot<T> : ActivityHandler
        where T : Dialog
    {
        protected readonly Dialog Dialog;
        protected readonly BotState ConversationState;
        protected readonly BotState UserState;
        protected readonly ILogger Logger;

        public DialogBot(ConversationState conversationState, UserState userState, T dialog, ILogger<DialogBot<T>> logger)
        {
            ConversationState = conversationState;
            UserState = userState;
            Dialog = dialog;
            Logger = logger;
        }

        public override async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default(CancellationToken))
        {
            await base.OnTurnAsync(turnContext, cancellationToken);

            // Save any state changes that might have occured during the turn.
            await ConversationState.SaveChangesAsync(turnContext, false, cancellationToken);
            await UserState.SaveChangesAsync(turnContext, false, cancellationToken);
        }

        protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            Logger.LogInformation("Running dialog with Message Activity.");
            try
            {

                var activity = turnContext.Activity;
                if (activity.Value != null)
                {
                    var data = activity.Value.ToString();
                    JObject o = JObject.Parse(data);
                    await turnContext.SendActivityAsync("Welcome " + o["myName"]);
                    await turnContext.SendActivityAsync("How Can I Help you?");
                    return;
                }
                IMessageActivity reply = null;
                int flag = 1;

                

                await turnContext.SendActivityAsync("Sorry, I  with this image");
                //var connector = new ConnectorClient(new Uri(activity.ServiceUrl));
                // Run the Dialog with the new message Activity.
                if (activity.Attachments != null && activity.Attachments.Any())
                {
                    // We know the user is sending an attachment as there is at least one item
                    await turnContext.SendActivityAsync("Sorry, I can’t make out the car ");
                    // in the Attachments list.
                    try
                    {
                        DBAccess db = new DBAccess();
                        await turnContext.SendActivityAsync(" make out the car model with this image");
                        DataTable dt = db.Select_Flag();
                        await turnContext.SendActivityAsync("I can’t make out the car model with this image");
                        foreach (DataRow dr in dt.Rows)
                        {
                            if (dr["flag"].Equals(0))
                            {
                                flag = 0;
                            }
                            else
                            {
                                flag = 1;

                            }
                        }

                        if (flag == 0)
                        {
                            try
                            {

                                await turnContext.SendActivityAsync("Sorry, I can’t make out the car model with this image");

                                db.Update_Flag();
                                await Task.Delay(3000);

                                await turnContext.SendActivityAsync("Please can you upload your car-picture again?");
                                // await turnContext.SendActivityAsync("Oh! This image sees off the context….Please can you check and upload the issue-screenshot?");
                                return;
                            }
                            catch (Exception e)
                            {
                                await turnContext.SendActivityAsync(e.ToString());
                                return;
                            }
                        }
                        else
                        {
                            await turnContext.SendActivityAsync("This looks great ! ");
                            await Task.Delay(3000);
                            db.Update_Zero_Flag();
                            await turnContext.SendActivityAsync("Thanks Jessica for the information, Let me append the request for this too. Our Insurance-Advisor shall help you assess your insurance requirement for the bundle. We look forward to be your partner to mitigate your risks.");

                            await Task.Delay(3000);

                            await turnContext.SendActivityAsync("Just to affirm: Graham Smith (Reachable over +1 223.233.2341) shall be calling you tomorrow (23 March, 2020) between 9 AM – 11 AM to take the conversation further");
                            await turnContext.SendActivityAsync("Have a nice day.");
                            return;
                        }
                    }
                    catch (SqlException e)
                    {

                        await turnContext.SendActivityAsync("Have a nice day.");
                        await turnContext.SendActivityAsync(e.ToString());
                        return;
                    }





                }
                else
                {





                    // var reply = ProcessInput(turnContext);
                    //await turnContext.SendActivityAsync(reply, cancellationToken);
                    // Run the Dialog with the new message Activity.
                    await Dialog.RunAsync(turnContext, ConversationState.CreateProperty<DialogState>("DialogState"), cancellationToken);
                }
            }
            catch(Exception e)
            {
                await turnContext.SendActivityAsync(e.ToString());
            }
        }
        private static async Task<Stream> GetImageStream(ConnectorClient connector, Attachment imageAttachment)
        {
            using (var httpClient = new HttpClient())
            {

                var uri = new Uri(imageAttachment.ContentUrl);

                return await httpClient.GetStreamAsync(uri);
            }
        }
    }

    

    public class RootObject
    {
        public string status { get; set; }
        public List<RecognitionResult> recognitionResult { get; set; }
    }

    public class RootObjec
    {

        public List<RecognitionResult> recognitionResult { get; set; }
    }
    public class Word
    {
        public List<int> boundingBox { get; set; }
        public string text { get; set; }
    }

    public class Line
    {
        public List<int> boundingBox { get; set; }
        public string text { get; set; }
        public List<Word> words { get; set; }
    }

    public class RecognitionResult
    {
        public List<Line> lines { get; set; }
    }

}
