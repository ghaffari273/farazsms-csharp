# FarazSMS (.NET)

**FarazSMS · IranPayamak** — فراز اس ام اس · ایران پیامک

Official .NET client for the FarazSMS / IranPayamak web services.

- Website: [https://farazsms.com](https://farazsms.com)
- Panel: [https://iranpayamak.com](https://iranpayamak.com)

Wraps all 63 endpoints — pattern/OTP sending, bulk SMS, reports, phonebook, and more. Uses only `System.Net.Http.HttpClient` and `System.Text.Json` — no external dependencies.

## Install

```bash
dotnet add package FarazSMS
```

## Quick start

```csharp
using System;
using System.Threading.Tasks;
using FarazSMS;

class Program
{
    static async Task Main()
    {
        var client = new FarazSMS("YOUR_API_KEY");

        // Account balance
        var balance = await client.Balance();
        Console.WriteLine(balance);

        // Send an OTP / pattern message
        await client.SendPattern(
            code: "abc123",
            recipient: "09123456789",
            attributes: new { code = "5821", name = "Ali" });

        // Send a simple bulk message
        await client.SendSimple(
            "Hello from FarazSMS!",
            new[] { "09123456789", "09120000000" });

        try
        {
            await client.Balance();
        }
        catch (FarazException ex)
        {
            Console.WriteLine($"Error {ex.StatusCode}: {ex.Message}");
        }
    }
}
```

Authentication uses the `Api-Key` header (case-sensitive). The default sender line is `90008361`, and every send includes `"number_format": "english"`.

## Methods

| Category | Method | HTTP | Endpoint |
| --- | --- | --- | --- |
| Low-level | `Request(method, path, body, query)` | * | any |
| Account | `Balance()` | GET | `/ws/v1/account/balance` |
| Account | `Profile()` | GET | `/ws/v1/account/profile` |
| Account | `Lines()` | GET | `/ws/v1/lines/accessible` |
| Send | `SendPattern(code, recipient, attributes, line)` | POST | `/ws/v1/sms/pattern` |
| Send | `SendSimple(text, recipients, line)` | POST | `/ws/v1/sms/simple` |
| Send | `SendVariable(text, recipients, line)` | POST | `/ws/v1/sms/keywords` |
| Patterns | `CreatePattern(payload)` | POST | `/ws/v1/patterns` |
| Patterns | `Patterns(query)` | GET | `/ws/v1/patterns` |
| Reports | `Inbox(page, limit)` | GET | `/ws/v1/inbox` |
| Reports | `SendRequests(query)` | GET | `/ws/v1/send_request` |
| Reports | `SendRequestItems(id, query)` | GET | `/ws/v1/send_request/{id}/items` |
| Phonebook | `Phonebooks()` | GET | `/ws/v1/phone_book` |
| Phonebook | `AddContact(payload)` | POST | `/ws/v1/phone_book_data` |
| Reference | `Provinces()` | GET | `/provinces` |
| Reference | `NumberBanks()` | GET | `/ws/v1/number_bank` |

All methods return `Task<JsonElement>` and throw `FarazException` (carrying the HTTP status code and message) on an error envelope.

## License

MIT — see [LICENSE](LICENSE).
