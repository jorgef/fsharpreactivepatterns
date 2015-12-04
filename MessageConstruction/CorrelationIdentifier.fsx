#load "..\References.fsx"

open Akka.Actor

type PriceQuote = { QuoterId: string; RetailerId: string; RfqId: string; ItemId: string; RetailPrice: decimal; DiscountPrice: decimal }
type RequestPriceQuote = RequestPriceQuote of rfqId: string * itemId: string * retailPrice: decimal * orderTotalRetailPrice: decimal
type PriceQuoteTimedOut = PriceQuoteTimedOut of rfqId: string
type RequiredPriceQuotesForFulfillment = RequiredPriceQuotesForFulfillment of rfqId: string * quotesRequested: int
type QuotationFulfillment = QuotationFulfillment of rfqId: string * quotesRequested: int * priceQuotes: PriceQuote seq * requester: IActorRef
type BestPriceQuotation = BestPriceQuotation of rfqId: string * priceQuotes: PriceQuote seq