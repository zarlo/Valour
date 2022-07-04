﻿/*  Valour - A free and secure chat client
 *  Copyright (C) 2021 Vooper Media LLC
 *  This program is subject to the GNU Affero General Public license
 *  A copy of the license should be included - if not, see <http://www.gnu.org/licenses/>
 */

namespace Valour.Server.Database.Items.Users;

/// <summary>
/// The credential class allows different authentication types to work
/// together in a clean and organized way
/// </summary>
[Table("credentials")]
public class Credential
{
    [ForeignKey("UserId")]
    public virtual User User { get; set; }

    /// <summary>
    /// The ID of this credential
    /// </summary>
    [Column("id")]
    public ulong Id { get; set; }

    /// <summary>
    /// The ID of the user using this credential
    /// </summary>
    [Column("user_id")]
    public ulong UserId { get; set; }

    /// <summary>
    /// The type of credential. This could be password, google, or whatever
    /// way the user is signing in
    /// </summary>
    [Column("credential_type")]
    public string CredentialType { get; set; }

    /// <summary>
    /// This is what identified the user - in the case of normal logins,
    /// this would be the email used to log in.
    /// </summary>
    [Column("identifier")]
    public string Identifier { get; set; }

    /// <summary>
    /// The secret that allows the login - this would be the password
    /// hash for a normal login. This should NOT be able to be reached by the client.
    /// If password hash, should be 32 bytes (256 bits)
    /// </summary>
    [Column("secret")]
    public byte[] Secret { get; set; }

    /// <summary>
    /// The unique salt for the password.
    /// Not to be confused with league of legends players.
    /// This only really applies to a password login.
    /// </summary>
    [Column("salt")]
    public byte[] Salt { get; set; }
}

/// <summary>
/// Contains all the credential type names
/// </summary>
public class CredentialType
{
    public const string PASSWORD = "Password";
}

