﻿module FSharpx.Tests.ValidationTests

open System
open FSharpx
open FSharpx.CSharpTests
open FSharpx.Either
open FSharpx.Validation
open NUnit.Framework
open Microsoft.FSharp.Core

let validator pred error value =
    if pred value
        then Choice1Of2 value
        else Choice2Of2 [error]

let (==) = LanguagePrimitives.PhysicalEquality
let inline (!=) a b = not (a == b)

let nonNull e = validator ((!=) null) e
let notEqual a = validator ((<>) a)

let validateAddressLines =
    validator 
        (fun (a: Address) -> a.Line1 != null || a.Line2 == null) 
        "Line1 is empty but Line2 is not"

//let inline konst a _ = a
//let inline konst2 a _ _ = a

let validateAddress (a: Address) = 
    returnM a
    <* nonNull "Post code can't be null" a.Postcode
    <* validateAddressLines a

open FSharpx.Nullable

let greaterThan o = validator ((<?) o)

let validateOrder (o: Order) =
    let nameNotNull = nonNull "Product name can't be null" o.ProductName
    let positiveCost n = greaterThan (0m).n (sprintf "Cost for product '%s' can't be negative" n) o.Cost
    nameNotNull >>= positiveCost |> Either.map (fun _ -> o)

(*    validation {
        let! name = nonNull "Product name can't be null" o.ProductName
        let! _ = greaterThan (0m).n (sprintf "Cost for product '%s' must be positive" name) o.Cost
        return o
    } *)
    

let validateOrders c = seqValidator validateOrder c
    
[<Test>]
let ValidateCustomer() = 
    let customer = 
        Customer(
            Surname = "foo",
            Address = Address(Postcode = "1424"),
            Orders = ResizeArray([
                                    Order(ProductName = "Foo", Cost = (5m).n)
                                    Order(ProductName = "Bar", Cost = (-1m).n)
                                    Order(ProductName = null , Cost = (-1m).n)
                     ]))
    let result = 
        returnM customer
        <* nonNull "Surname can't be null" customer.Surname
        <* notEqual "foo" "Surname can't be foo" customer.Surname
        <* validateAddress customer.Address
        <* validateOrders customer.Orders
    match result with
    | Choice1Of2 c -> failwithf "Valid customer: %A" c
    | Choice2Of2 errors -> printfn "Invalid customer. Errors:\n%A" errors

[<Test>]
let ``using ap``() =
  let customer = Customer()
  let result = 
    returnM (fun _ _ -> customer)
    |> Validation.ap (nonNull "Surname can't be null" customer.Surname)
    |> Validation.ap (notEqual "foo" "Surname can't be foo" customer.Surname)
  match result with
  | Choice1Of2 c -> failwithf "Valid customer: %A" c
  | Choice2Of2 errors -> printfn "Invalid customer. Errors:\n%A" errors

[<Test>]
let ``validation with monoid``() =
  let v = Validation.CustomValidation(Monoid.IntSumMonoid())
  // count the number of broken rules
  let validator pred value =
      if pred value
          then Choice1Of2 value
          else Choice2Of2 (Monoid.Sum 1)
  let notEqual a = validator ((<>) a)
  let lengthNotEquals l = validator (fun (x: string) -> x.Length <> l)
  let validateString x = 
    Either.returnM x
    |> v.apl (notEqual "hello" x)
    |> v.apl (lengthNotEquals 5 x)
  match validateString "hello" with
  | Choice1Of2 c -> failwithf "Valid string: %s" c
  | Choice2Of2 (Monoid.Sum e) -> Assert.AreEqual(2, e)
