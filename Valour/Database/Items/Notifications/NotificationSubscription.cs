﻿using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using Valour.Database.Items.Users;
using Valour.Shared.Items.Notifications;

/*  Valour - A free and secure chat client
 *  Copyright (C) 2021 Vooper Media LLC
 *  This program is subject to the GNU Affero General Public license
 *  A copy of the license should be included - if not, see <http://www.gnu.org/licenses/>
 */

namespace Valour.Database.Items.Notifications;

public class NotificationSubscription : NotificationSubscriptionBase
{
    [ForeignKey("User_Id")]
    public User User { get; set; }
}