using System.Security.Cryptography;
using WebPush;

var vapid = VapidHelper.GenerateVapidKeys();
var adminKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

Console.WriteLine("Spara värdena säkert. Lägg aldrig den privata nyckeln eller adminnyckeln i Git.");
Console.WriteLine();
Console.WriteLine($"VapidPublicKey:  {vapid.PublicKey}");
Console.WriteLine($"VapidPrivateKey: {vapid.PrivateKey}");
Console.WriteLine($"AdminKey:        {adminKey}");

