// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;
using Microsoft.Recognizers.Text.DataTypes.TimexExpression;

namespace Microsoft.BotBuilderSamples.Dialogs
{
    public class MainDialog : ComponentDialog
    {
        private readonly FlightBookingRecognizer _luisRecognizer;
        protected readonly ILogger Logger;

        // Dependency injection uses this constructor to instantiate MainDialog
        public MainDialog(FlightBookingRecognizer luisRecognizer, BookingDialog bookingDialog, ILogger<MainDialog> logger)
            : base(nameof(MainDialog))
        {
            _luisRecognizer = luisRecognizer;
            Logger = logger;

            AddDialog(new TextPrompt(nameof(TextPrompt)));
            AddDialog(bookingDialog);
            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[]
            {
                //IntroStepAsync,
                ActStepAsync,
               // FinalStepAsync,
            }));

            // The initial child Dialog to run.
            InitialDialogId = nameof(WaterfallDialog);
        }

        private async Task<DialogTurnResult> IntroStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            if (!_luisRecognizer.IsConfigured)
            {
                await stepContext.Context.SendActivityAsync(
                    MessageFactory.Text("NOTE: LUIS is not configured. To enable all capabilities, add 'LuisAppId', 'LuisAPIKey' and 'LuisAPIHostName' to the appsettings.json file.", inputHint: InputHints.IgnoringInput), cancellationToken);

                return await stepContext.NextAsync(null, cancellationToken);
            }

            // Use the text provided in FinalStepAsync or the default if it is the first time.
            var messageText = stepContext.Options?.ToString() ?? "What can I help you with today?\nSay something like \"Book a flight from Paris to Berlin on March 22, 2020\"";
            var promptMessage = MessageFactory.Text(messageText, messageText, InputHints.ExpectingInput);
            return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions { Prompt = promptMessage }, cancellationToken);
        }

        private async Task<DialogTurnResult> ActStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            if (!_luisRecognizer.IsConfigured)
            {
                // LUIS is not configured, we just run the BookingDialog path with an empty BookingDetailsInstance.
                return await stepContext.BeginDialogAsync(nameof(BookingDialog), new BookingDetails(), cancellationToken);
            }

            // Call LUIS and gather any potential booking details. (Note the TurnContext has the response to the prompt.)
            var luisResult = await _luisRecognizer.RecognizeAsync<FlightBooking>(stepContext.Context, cancellationToken);
            switch (luisResult.TopIntent().intent)
            {
                case FlightBooking.Intent.BookFlight:
                    await ShowWarningForUnsupportedCities(stepContext.Context, luisResult, cancellationToken);

                    // Initialize BookingDetails with any entities we may have found in the response.
                    var bookingDetails = new BookingDetails()
                    {
                        // Get destination and origin from the composite entities arrays.
                        Destination = luisResult.ToEntities.Airport,
                        Origin = luisResult.FromEntities.Airport,
                        TravelDate = luisResult.TravelDate,
                    };

                    // Run the BookingDialog giving it whatever details we have from the LUIS call, it will fill out the remainder.
                    return await stepContext.BeginDialogAsync(nameof(BookingDialog), bookingDetails, cancellationToken);

                case FlightBooking.Intent.GetWeather:
                    // We haven't implemented the GetWeatherDialog so we just display a TODO message.
                    var getWeatherMessageText = "TODO: get weather flow here";
                    var getWeatherMessage = MessageFactory.Text(getWeatherMessageText, getWeatherMessageText, InputHints.IgnoringInput);
                    await stepContext.Context.SendActivityAsync(getWeatherMessage, cancellationToken);
                    break;
                case FlightBooking.Intent.Introduction:
                    await stepContext.Context.SendActivityAsync("Hi Jessica, It’s a pleasure to serve you.  May I please have the number that the account is associated with ?");
                    break;
                case FlightBooking.Intent.SecondLevel:
                    HeroCard secondlevel = new HeroCard()
                    {
                        Text = "Oh no problem. Can you please select the area in which you are looking help for?",
                        Buttons = new List<CardAction>
                         {
                    new CardAction(ActionTypes.PostBack, "Home Insurance", value: "Looking for Home Insurance"),
                    new CardAction(ActionTypes.PostBack, "Auto Insurance", value: "Looking for Auto Insurance"),
                    new CardAction(ActionTypes.PostBack, "Health Insurance ", value: "Looking for Health Insurance "),
                    new CardAction(ActionTypes.PostBack, "Others", value: "Looking for Others")

                        }
                    };

                    var secondlevelreply = MessageFactory.Attachment(secondlevel.ToAttachment());
                    await stepContext.Context.SendActivityAsync(secondlevelreply, cancellationToken);
                    break;

                case FlightBooking.Intent.HomeInsurance:
                    await stepContext.Context.SendActivityAsync("Wonderful.I’d be happy to help you with that.  I’m here to help you find the best price for your home insurance. In order to proceed can you please help us with your Property location and Zip Code");
                    break;
                case FlightBooking.Intent.Zipcode:
                    await stepContext.Context.SendActivityAsync("Okay Jessica. Let me find the best rates for you.");
                    await Task.Delay(3000);
                    await stepContext.Context.SendActivityAsync("We have passed your requirement to our Actuary & Insurance-Advisor. Here is the details of the Insurance-Advisor assigned to help you with the Quote.");

                    await Task.Delay(3000);
                    await stepContext.Context.SendActivityAsync("Graham Smith has been assigned to assist you with your quote. He is reachable on +1 223.233.2341. He shall be getting back to you within 48 hrs.");
                    HeroCard zipcode = new HeroCard()
                    {
                        Text = "Please select your preferred slot for the Insurance-Advisor to call you?",
                        Buttons = new List<CardAction>
                         {
                    new CardAction(ActionTypes.PostBack, "9 AM – 11 AM", value: "Time Slot is 9 to 11"),
                    new CardAction(ActionTypes.PostBack, "2 PM – 4 PM", value: "Time Slot is 9 to 11"),
                    new CardAction(ActionTypes.PostBack, "5 PM – 6 PM", value: "Time Slot is 9 to 11 "),
                    new CardAction(ActionTypes.PostBack, "6 PM - 7 PM", value: "Time Slot is 9 to 11")

                        }
                    };

                    var zipcodereply = MessageFactory.Attachment(zipcode.ToAttachment());
                    await stepContext.Context.SendActivityAsync(zipcodereply, cancellationToken);

                    break;
                case FlightBooking.Intent.Timeslot:
                    await stepContext.Context.SendActivityAsync("Thanks Jessica for selecting the morning slot of 9 AM – 11 AM. We look forward to help you with the best quotation for insuring your house.");

                    await stepContext.Context.SendActivityAsync("While we are there..");

                    HeroCard timeslot = new HeroCard()
                    {
                        Text = "Well, we provide auto insurance to over 10 million drivers each year and have saved them an average of $500 by bundling their home and auto insurance.  Would you be interested in saving an extra 20% today??",
                        Buttons = new List<CardAction>
                         {
                    new CardAction(ActionTypes.PostBack, "YES", value: "Only Auto Insurance"),
                    new CardAction(ActionTypes.PostBack, "NO", value: "Both Home and Auto Insurance")

                        }
                    };

                    var timeslotreply = MessageFactory.Attachment(timeslot.ToAttachment());
                    await stepContext.Context.SendActivityAsync(timeslotreply, cancellationToken);
                    break;
             
                    
                case FlightBooking.Intent.Homenauto:
                    HeroCard Homenauto = new HeroCard()
                    {
                        Text = "Jessica, this week, we are running a multi policy discount . We believe you could save upto 30% in bundling your Home and Auto insurance? Would you like to know more?",
                        Buttons = new List<CardAction>
                         {
                    new CardAction(ActionTypes.PostBack, "YES", value: "Yes both Insurances."),
                    new CardAction(ActionTypes.PostBack, "NO", value: "Only home Insurance"),
                    new CardAction(ActionTypes.PostBack, "Get back to me later", value: "Get back to me later.")
                    
                        }
                    };

                    var Homenautoreply = MessageFactory.Attachment(Homenauto.ToAttachment());
                    await stepContext.Context.SendActivityAsync(Homenautoreply, cancellationToken);
                    break;
                case FlightBooking.Intent.Model:
                    HeroCard Model = new HeroCard()
                    {
                        Text = "Thanks Jessica .What maker of the car do you want to insure ?",
                        Buttons = new List<CardAction>
                         {
                    new CardAction(ActionTypes.PostBack, "Ferrari", value: "Car Models"),
                    new CardAction(ActionTypes.PostBack, "BMW", value: "Car Models"),
                    new CardAction(ActionTypes.PostBack, "KIA", value: "Car Models"),
                    new CardAction(ActionTypes.PostBack, "Others", value: "Car Models")

                        }
                    };

                    var Modelreply = MessageFactory.Attachment(Model.ToAttachment());
                    await stepContext.Context.SendActivityAsync(Modelreply, cancellationToken);
                    break;

                case FlightBooking.Intent.Year:
                    HeroCard Year = new HeroCard()
                    {
                        Text = "Can you please select the period of Manufacture",
                        Buttons = new List<CardAction>
                         {
                    new CardAction(ActionTypes.PostBack, "2018-19", value: "Manufacturing Year"),
                    new CardAction(ActionTypes.PostBack, "2016-18", value: "Manufacturing Year"),
                    new CardAction(ActionTypes.PostBack, "2014-16", value: "Manufacturing Year"),
                    new CardAction(ActionTypes.PostBack, "Others", value: "Manufacturing Year")

                        }
                    };

                    var Yearreply = MessageFactory.Attachment(Year.ToAttachment());
                    await stepContext.Context.SendActivityAsync(Yearreply, cancellationToken);
                    break;

                case FlightBooking.Intent.Photocar:
                    HeroCard Photocar = new HeroCard()
                    {
                        Text = "Do you have a photo of the car ?",
                        Buttons = new List<CardAction>
                         {
                    new CardAction(ActionTypes.PostBack, "YES", value: "Select yes to the car photo"),
                    new CardAction(ActionTypes.PostBack, "NO", value: "Select No")

                        }
                    };

                    var Photocarreply = MessageFactory.Attachment(Photocar.ToAttachment());
                    await stepContext.Context.SendActivityAsync(Photocarreply, cancellationToken);
                    break;
                case FlightBooking.Intent.Uploadnoncar:
                    await stepContext.Context.SendActivityAsync("Please click the image upload button to select your image.");

                    break;

                case FlightBooking.Intent.Sendoff:
                    await stepContext.Context.SendActivityAsync("Thanks Jessica for the information, Let me append the request for this too. Our Insurance-Advisor shall help you assess your insurance requirement for the bundle. We look forward to be your partner to mitigate your risks.");

                    await Task.Delay(3000);

                    await stepContext.Context.SendActivityAsync("Just to affirm: Graham Smith (Reachable over +1 223.233.2341) shall be calling you tomorrow (23 March, 2020) between 9 AM – 11 AM to take the conversation further");
                    await stepContext.Context.SendActivityAsync("Have a nice day.");
                    break;
                default:
                    // Catch all for unhandled intents
                    var didntUnderstandMessageText = $"Sorry, I didn't get that. Please try asking in a different way (intent was {luisResult.TopIntent().intent})";
                    var didntUnderstandMessage = MessageFactory.Text(didntUnderstandMessageText, didntUnderstandMessageText, InputHints.IgnoringInput);
                    await stepContext.Context.SendActivityAsync(didntUnderstandMessage, cancellationToken);
                    break;
            }

            return await stepContext.NextAsync(null, cancellationToken);
        }

        // Shows a warning if the requested From or To cities are recognized as entities but they are not in the Airport entity list.
        // In some cases LUIS will recognize the From and To composite entities as a valid cities but the From and To Airport values
        // will be empty if those entity values can't be mapped to a canonical item in the Airport.
        private static async Task ShowWarningForUnsupportedCities(ITurnContext context, FlightBooking luisResult, CancellationToken cancellationToken)
        {
            var unsupportedCities = new List<string>();

            var fromEntities = luisResult.FromEntities;
            if (!string.IsNullOrEmpty(fromEntities.From) && string.IsNullOrEmpty(fromEntities.Airport))
            {
                unsupportedCities.Add(fromEntities.From);
            }

            var toEntities = luisResult.ToEntities;
            if (!string.IsNullOrEmpty(toEntities.To) && string.IsNullOrEmpty(toEntities.Airport))
            {
                unsupportedCities.Add(toEntities.To);
            }

            if (unsupportedCities.Any())
            {
                var messageText = $"Sorry but the following airports are not supported: {string.Join(',', unsupportedCities)}";
                var message = MessageFactory.Text(messageText, messageText, InputHints.IgnoringInput);
                await context.SendActivityAsync(message, cancellationToken);
            }
        }

        //private async Task<DialogTurnResult> FinalStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        //{
        //    // If the child dialog ("BookingDialog") was cancelled, the user failed to confirm or if the intent wasn't BookFlight
        //    // the Result here will be null.
        //    if (stepContext.Result is BookingDetails result)
        //    {
        //        // Now we have all the booking details call the booking service.

        //        // If the call to the booking service was successful tell the user.

        //        var timeProperty = new TimexProperty(result.TravelDate);
        //        var travelDateMsg = timeProperty.ToNaturalLanguage(DateTime.Now);
        //        var messageText = $"I have you booked to {result.Destination} from {result.Origin} on {travelDateMsg}";
        //        var message = MessageFactory.Text(messageText, messageText, InputHints.IgnoringInput);
        //        await stepContext.Context.SendActivityAsync(message, cancellationToken);
        //    }

        //    // Restart the main dialog with a different message the second time around
        //    var promptMessage = "What else can I do for you?";
        //    return await stepContext.ReplaceDialogAsync(InitialDialogId, promptMessage, cancellationToken);
        //}
    }
}
