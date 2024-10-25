// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Linq;
using Ghosts.Animator.Extensions;
using Ghosts.Animator.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Ghosts.Animator
{
    public static class CreditCard
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public enum CardType
        {
            Visa = 0,
            MasterCard = 1,
            DinersClub = 2
        }

        public static CardType GetCardType()
        {
            var o = Enum.GetValues(typeof(CardType)).Cast<CardType>().ToList();
            return o.RandomElement();
        }

        private static readonly string[] DINERS_PREFIX_IIN_RANGES = new[] { "300", "301", "302", "303", "36", "38" };
        private static readonly string[] MASTERCARD_PREFIX_IIN_RANGES = new[] { "51", "52", "53", "54", "55" };
        private static readonly string[] VISA_PREFIX_IIN_RANGES = new[] { "4539", "4556", "4916", "4532", "4929", "40240071", "4485", "4716", "4" };

        private const short VISA_LENGTH = 16;
        private const short MASTER_LENGTH = 16;
        private const short DINERS_CLUB_LENGTH = 16;

        public static string CreditCardNumber(CardType type)
        {
            switch (type)
            {
                case CardType.Visa:
                    return CreateCreditCardNumber(VISA_PREFIX_IIN_RANGES, VISA_LENGTH);
                case CardType.MasterCard:
                    return CreateCreditCardNumber(MASTERCARD_PREFIX_IIN_RANGES, MASTER_LENGTH);
                case CardType.DinersClub:
                    return CreateCreditCardNumber(DINERS_PREFIX_IIN_RANGES, DINERS_CLUB_LENGTH);
                default:
                    throw new ArgumentException("Invalid credit card type");
            }
        }

        public static IEnumerable<FinancialProfile.CreditCard> GetCreditCards()
        {
            var list = new List<FinancialProfile.CreditCard>();
            for (var i = 0; i < AnimatorRandom.Rand.Next(1, 5); i++)
            {
                list.Add(GetCreditCard());
            }

            return list;
        }

        public static FinancialProfile.CreditCard GetCreditCard()
        {
            var t = GetCardType();

            var card = new FinancialProfile.CreditCard
            {
                Type = t.ToString(),
                Number = CreditCardNumber(t)
            };
            return card;
        }

        public static double GetNetWorth()
        {
            return Convert.ToDouble(AnimatorRandom.Rand.Next(-10000, 100000));
        }

        public static double GetTotalDebt()
        {
            return Convert.ToDouble(AnimatorRandom.Rand.Next(10000, 100000));
        }

        private static string FakeCreditCardNumber(string prefix, int length)
        {
            var creditCardNumber = prefix;
            int sum = 0, pos = 0;

            while (creditCardNumber.Length < (length - 1))
            {
                var randomNumber = (new Random().NextDouble() * 1.0f - 0f);
                creditCardNumber += Math.Floor(randomNumber * 10);
            }

            var creditCardNumberReversed = creditCardNumber.ToCharArray().Reverse();
            var creditCardNumbers = creditCardNumberReversed.Select(c => Convert.ToInt32(c.ToString()));

            var number = creditCardNumbers.ToArray();

            while (pos < length - 1)
            {
                var odd = number[pos] * 2;
                if (odd > 9) { odd -= 9; }

                sum += odd;

                if (pos != (length - 2)) { sum += number[pos + 1]; }

                pos += 2;
            }

            var validDigit = Convert.ToInt32((Math.Floor((decimal)sum / 10) + 1) * 10 - sum) % 10;

            creditCardNumber += validDigit;

            return creditCardNumber;
        }


        private static string CreateCreditCardNumber(string[] prefix, int length)
        {
            var random = new Random().Next(0, prefix.Length - 1);
            if (random > 1) { random--; }
            var creditCardNumber = prefix[random];
            creditCardNumber = FakeCreditCardNumber(creditCardNumber, length);
            return creditCardNumber;
        }

    }
}

