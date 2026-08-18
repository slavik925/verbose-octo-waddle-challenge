# Selenium Challenge

A .NET 10 console application that discovers the challenge credentials and
collects 50 authorization tokens under one server-issued ChallengeID.

## Prerequisites

- .NET 10 SDK
- Google Chrome
- Tor (`brew install tor` on macOS), or a `TOR_BINARY` environment variable
containing the absolute path to the Tor executable

## Run

Discover the credentials from the supplied `[1-3]{1,4}` constraint, then collect
the tokens:

```bash
dotnet run
```

```bash
dotnet run -- --known
```

The application writes the ChallengeID, token count, and numbered tokens to
`tokens.txt` after every successful authorization. A completed result is already
included in that file.

## Approach

- Selenium drives a visible Chrome browser; no direct login HTTP requests are used.
- Eight Tor processes warm in parallel while Chrome collects the first ten tokens.
- Each browser generation uses a distinct Tor exit subnet and rotates its user
agent, headers, viewport, locale, timezone, and canvas/WebGL fingerprint.
- The encrypted ASP.NET session cookie is transferred with Chrome DevTools so a
browser handoff retains the same ChallengeID and preserves its host-only scope.
- Form fields are selected by semantic input type because the challenge renames
them after occurrence 31.
- From occurrence 41 onward, Selenium generates mouse movement and verifies the
page's `/mm` request before submitting the form.
- Blocked exits and incomplete responses trigger a browser/proxy handoff without
advancing the requested token count.



## Results:

ChallengeId: 397587095
Count: 50

```
1. fEgkV8bVymNvOqiTBlMgbmnRVTux6CfsQUqIcqZrNc6fPYRXWFXnJqCZl+MC81QXy4/hpy4qHTG6PGOzWWJomA==
2. Q1ngnkxmYBus6m51MV+lJ2jNR2mZwhkwiOEx0/ECUdtHPjnTvBxwCS5Fek4gR2z5Hilk2lR7uNGO7DS3eOAYDg==
3. A4NPfD16fjhrDmNOy9N6xf0T7M0UR4pA96C3U2t0fT4IsHTiReyPiuT4EIv4XO2IV1ECWq7tjglOCJQvtqdDNQ==
4. vi6RzawOFCuhnFn352P1w5nYYxBEgSGql2AFPYWly7O2Av4ljSsUC6ZQEwz4gkK4oPYwz0ig5UyfuAKY7b2buQ==
5. kk+JIZgVLdCor9A8CEXESTAu5a1pSQYchEo+uc+ecq1I2ODXF8gM4zVoBWwP6tKBLhKu9RwlblenL1phfNL98A==
6. ug5rY6hwCxgRy1dF0cwNrHyOFTZIx3viMgJOpLkkWIyM/iXrPMyb082licJtUpBpxm/q7Rdj7+0PJ7WdpGYsRg==
7. F9fmC4JXiMdgnBRNcZRbuY/z21U50reJuu5y/loXEXjF+3vYUdI52QW/SlMTW+Dhiv8UD6xkHeF8S8AVPO0UTQ==
8. 7P0YOJDcnRb+8vvwOUNqrLhzAWdybx1S1jCpU1+5a3lhnxvcx7YFUf+x4kLS8G+9F0NrWruiMUQBHKMjCubnUw==
9. 2vtge+j7/igjE2Ld9etnA1H/Ime94QIvfLCdvAMI/t+XLAecCFLto7FcQOKsTEqsYVaw0iX5m0m2kwfMT1536w==
10. ta8DlRKKno66sGLRJVME9O5F4Mk/KjKnu7ygvQ6ZOWEcVJJ7SDXOQ7PvICvyQAuujMufwBvTrz7XPViyHt38yQ==
11. V6fjxK0wfF9tPByoq5L+0KWnm2DHdhIGEgTxo0fsU9O9RtplptjmLDIR0GXHjHHuex99Afp/WtTgpMpb3k3pvA==
12. Etc5AhHjD8FNvCKhQrQ28HjMnda3lSKaiO08Sd/uDOI6La78796SmjOt4VC3vzb4INg+asnpqSj3F7LN0vah5g==
13. WwD8tPSOIuqgAieZRsbLnclpNAqThZNe08R0tnq8xEHOpVZMBf7mFmMX4t9g0RYe9rXOYbzXFUHXSCX1f4fjmQ==
14. vIZoqEcsrhGDD/zHtFDmhx8UBw9abMzQtpOPu9o4uiepuASZDJYbCZ16+1aHL34/nN5vD0Z4OQMGlNXka0JZ8A==
15. YPECAA5g23KSAdHRenzxTG2TJ7qE8T9wce9MiGjCWNWoUXJcOV/osOi9+NNpQDIi8zhaqq/dNjr95RYbAOIzCA==
16. PGCR1iS+CKrN4aM7y/D1okY0jeW1NN0I16fM1PmbA3bVc38+BVWknQQIrFcfTfJiaU/4zunG7TFLTW1CHxbyiA==
17. Jjpp3wp1d86nTc50EXcwv5CPKvtDRstkcDXGiKQzyVW1NJWxvhtAq0bQOlGQsDI9Hcx1vPENVRssGeEONIzUBw==
18. 5Ng/1aH+qkci9MzGJPVQih3E6V4NCPlE1FvBTqydLOC146uv+X2JtimkisBU5r8bqvGHQTZjYEL10TvhOLHzXg==
19. vO5hGQn+8UUk6tFYFCPwLgWtGqKihopWPXbTAbFDujXW7r6YRgOrtr6FrYLd+3el00ukJ4HUVkSauEVQyl/e1Q==
20. uiWpY24roNiZgxHS2aQ25niOBjBzEQ4nOEZEhi0ZQSbxkbjdOvbdJCp3QGQa8AMt2lOw8GHPAg1h1t9dt2iOug==
21. FEs8cduvH+t4WpT8vU7P/VkBoD6A0jpVESCSqvIU2nKng/8AOqK/aD+V5FuYn4h9UpgZwVW8/5of2iVydb3M1g==
22. HRP8hcBTeQ7Mpz6lPaZC+ta0mzuBFt0J4cGTk4dgdOv4a6UCBtuo/Sr6eIzTWFBNZU0MKwDHDWplNOvsBAnFjA==
23. uT/quXkWmgPC6HyGDzWXr1oFmzXlTeKJgs99GYbZu5lhPUY5D5+ktvRqaZ8jt/5ZLOqg9c/GuhmwzL66eCtTIQ==
24. Ec51GOTAsVUVuY4wWOlCpiqWVxQZUNCcw0hCoPyrTBXcweQJDxwgmNZrdLWzUb/UCQhyJwszvkqOioT8/+Z34Q==
25. UPJFHvJ5NJjnYJKDUDXEzAUm/djAmHKQsjP4qEOCNsPsDXbCBjYfSG8uR1l3VQjMprfAoJI7IdHF8C0SUDNhCg==
26. baOttIp3e6HgTpjg9egydwOIvbZaZl8xT4reWaSVHU7HFsmjNh/g+sna3TG2uQDUNjnNPmdWBW7w+Ua90XvPyQ==
27. Mxsvn+SnDwvwsDjc6WjVPEpLVl/flr9nu885p3aJUWGhFG4eWnO3L58WpX8F0NtA/0NDrs0GSozXaqV/SEW3qQ==
28. FtLbojf9HJ0K8B3UuKMet3sZD8UE5NDLk76TGT0qLa7+WLRytu/VrnOjgiwGzaGeh484ULgWUdEgl/b2GzIvoA==
29. 662wh9gUWe8+lnQRz/jlhRQGiFGndgX34Kcn8J1ugGgqrVhk4McqJuKqqZvZhoViSDJ++sRU13CCeneJI/pTeQ==
30. jyCqLbIUKmgFXz5QUuCJMAjRkf+jg3C8SGHa36VjWjVjg3VrIp9ICqm9yn7N9gHggQJCx3qenDo1Tt5i4XInMw==
31. KEhNN0wxdJBF5H7j1ZKIfIlFKBpKcDMHW8ro27VDBzdy0Z0b5feND98bKj05sY4PTKA2FaMrmm487OaxfC0zLg==
32. qlRR5SjrnqhQb6r9Nj3MICCQOzCZgqqLITlIivJX7sqIb3A1dbfKbZ4IYQnELji18Sc8oawpR261J+7HsalLVg==
33. OYDo9JcZIgeghBuoovPb2YLLxuQclwSYZd9ZlD5x80nFWdML/uoyWgfhjm16Q4iDdWcwi7ViqBRGNSWl9zU+oA==
34. k9MZ7bwys/1DSZXG1HKC8k3z7shD3mChzoncsBTtg0GhvfLuU23G6DV6j4BRUTvY6YvVUqUdznlQopR7WKZdHQ==
35. szEhLuHM6ofVxr0/mLaDYdsSwdH0XuApz4FxWtKnz+3Te1PL2d4d7f5nxJEM0jDdvK6hmo8HHZHkM0ffNzQ4Vw==
36. RwElXxD3+CoKuilO58xVYkuf6hT6r8zTW9WoAgpt1QyfCI9kOq4oTctfiF5zRrWSdXY1zD8E5yf+LUStUefMKw==
37. 132uDOgpX5lntvt9Cb1c76YZLWtSoZKJa9TCdluHPQJOsTNqZsjVzRdlvFdBBcZI8aUzpnaDTEXpaaq2dD1mCw==
38. SCrGfe1+ePbe9j0QuVA6bIBVmkMcc6p0mluWjEmgEFypM+7exp0ba9p/wmnM4DHhvsVJEqI2gfg9auLosVV18g==
39. 6ZbJ/yWkroEMQ9tapltAPnHIu9RoC9dP3U9SAo1vJ8wXuciZnmWOtuwgPIqW4XU+e4EH0llosSeCjMfh/yYaIQ==
40. BPd+IdSAZ+ruUaabpIg2NcTvhjorU+o9Oh40iNyglcTWbgGc9B1//kOWTlL8F2BbnPLHFoGKYRCULpNMOKTAYw==
41. H2UhjOF21p2X4ko2APdPV8XPj1590DmEZo5olMKNhDpAIbjV9d4t9IPYmO9ZDT84KBqy1tDGwXtt1UZzhDUYzg==
42. t4lcmNUP22wiYSP9CiTR02MTMiIaptL1uypmTJXikGrWBsfmTslpH+PkxY/SIeEo+3lcvFr6nYIJ6hUBZlwtdA==
43. rzWiKP2i7rkknrvG5h3Slt4cEV/JplR9DPjIZDKUamizE/VV3qXhiNm7eM9OACTl3sHCsgOPHpsFqWs5lgfu+A==
44. X/RhPpBkDKoQ7yrGBieFhWttY3p+cC9nxlxX9PF9kb9dgalXmPW02gKNKMpzk8PBnpPid4T2D34jVUWZsouyfg==
45. lB8Ca405dNV5GWAcHqCgxlpRctbtR1ZVml3inSpuNrr9keG97x7YKshlsPZYcpVSqNcIJEbQSEyvIRf+/ms4FA==
46. XDdAK4p8EPJp9UkrzOp4yVU2X7zkS9DUURjX52WDE6Ne1OYttYB3r9WZRfMJj9VYuA7ltEH6QRc1pQ7XotxuXw==
47. poaBRC95yrmNWDMV80QhsM4O2GQ660JdUTRFzyw6iNbK0BsP5jvQcpSJLu/vFVsV+NwnnD5CdJeZZjEpAshL/Q==
48. kFGtBZJaYFnwQV85uKx3wnrDkyPLkxCdqCQIjbH45SOJuRfNWhBPPnWKH3NXtwDGXgQvzSDIUI3vdT982Cmf2Q==
49. mYzEGJddDFK8Jdbl051nBkWW71A5R0PYdcN6x8UuxwlQThH9svXJ2ztofft181ytIEDzll6O7+r2mfQAys+Hdw==
50. zP/FHZ1P93bSZleM2bYc4YlhMFb/hzDTz+51PU6ytPwOqSMVB32gDk2roZ5YTFYUYeY8gU0kn0LIFnB7cffobQ==
```

