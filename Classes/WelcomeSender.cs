using Flexinets.Core.Communication.Sms;
using FlexinetsDBEF;
using log4net;
using System;
using System.Globalization;
using System.Linq;

namespace Flexinets.Radius
{
    public class WelcomeSender
    {
        private readonly ILog _log = LogManager.GetLogger(typeof(WelcomeSender));
        private readonly FlexinetsEntitiesFactory _contextFactory;
        private readonly ISmsGateway _smsGateway;

        public WelcomeSender(FlexinetsEntitiesFactory contextFactory, ISmsGateway smsGateway)
        {
            _contextFactory = contextFactory;
            _smsGateway = smsGateway;
        }


        public void CheckWelcomeSms(String msisdn)
        {
            _log.Debug($"Checking welcome sms for msisdn {msisdn}");
            using (var db = _contextFactory.GetContext())
            {
                var currentsession = (from o in db.Onlines
                                      where o.Calling_Station_Id == msisdn
                                      select o).SingleOrDefault();

                if (currentsession == null)
                {
                    _log.Error($"Couldnt find current session for msisdn: {msisdn}");
                    return;
                }
                if (!currentsession.NetworkId.HasValue || currentsession.NetworkId.Value == 0)
                {
                    _log.Error($"Invalid networkid found for msisdn: {msisdn}");
                    return;
                }

                var simcard = (from o in db.SimCards
                               where o.Msisdn == msisdn
                               select o).SingleOrDefault();

                if (simcard == null)
                {
                    _log.Warn($"Couldnt find sim card with msisdn: {msisdn}");
                    return;
                }
                if (simcard.UserSetting == null)
                {
                    _log.Warn($"Sim card {simcard.Msisdn} not mapped to a user?!");
                    return;
                }

                _log.Debug($"Found sim card with msisdn: {simcard.Msisdn}");


                var network = (from o in db.PricesViews
                               where o.mccmnc == currentsession.NetworkId && o.PricelistId == simcard.UserSetting.user.directory.GroupSetting.PricelistId
                               select o).SingleOrDefault();

                if (network == null)
                {
                    _log.Error($"NetworkId {currentsession.NetworkId} not found?! Wtf?");
                    return;
                }

                // Check if there has been a session on the same network within a week
                var lastsession = (from o in db.Accountings
                                   orderby o.Event_Timestamp descending
                                   where o.Calling_Station_Id == simcard.Msisdn && o.Acct_Status_Type == "stop"
                                   select o).FirstOrDefault();

                if (lastsession == null)
                {
                    // Dont send an sms on the first connection, allow setup
                    _log.Debug($"Sim card {simcard.Msisdn}, previous NetworkId: n/a, current NetworkId: {currentsession.NetworkId}");
                    //SendWelcomeSms(simcard.UserSetting.user, network);
                }
                else if (lastsession.Event_Timestamp < DateTime.UtcNow.AddDays(-7))     // Over a week since the last session
                {
                    _log.Debug($"Sim card {simcard.Msisdn}, over 1 week since last connection, current NetworkId: {currentsession.NetworkId}");
                    SendWelcomeSms(simcard.UserSetting.user, network);
                }
                else if (lastsession.NetworkId.HasValue && lastsession.NetworkId.Value != currentsession.NetworkId)
                {
                    _log.Debug($"Sim card {simcard.Msisdn}, previous NetworkId: {lastsession.NetworkId}, current NetworkId: {currentsession.NetworkId}");
                    SendWelcomeSms(simcard.UserSetting.user, network);
                }
                else
                {
                    _log.Debug($"Sim card {simcard.Msisdn} on same network");
                }
            }
        }


        private void SendWelcomeSms(user user, PricesView network)
        {
            _log.Info("Sending welcome sms to " + user.phonenumber);

            var template = "FLEXINETS: Welcome to {{country}}\nYour subscription is now connected to {{network}}.\nTraffic price: {{price}}\nwww.flexinets.se";

            template = template.Replace("{{country}}", network.CommonName);
            template = template.Replace("{{network}}", network.providername);

            if (network.StepPrice != null)
            {
                template = template.Replace("{{price}}", network.StepPrice.Value.ToString("f2", CultureInfo.InvariantCulture) + "EUR / " + network.StepSize + "MB");
            }
            else
            {
                template = template.Replace("{{price}}", network.Price.ToString("f2", CultureInfo.InvariantCulture) + "EUR / MB");
            }


            var smsid = _smsGateway.SendSmsAsync(template, user.phonenumber).Result;

            _log.Info($"Sent sms with id: {smsid} to {user.phonenumber}");
        }
    }
}