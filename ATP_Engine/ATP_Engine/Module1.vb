Imports System.Data.SqlClient
Imports Newtonsoft.Json.Linq
Imports System.Net
Imports System.Threading
Imports ATP_Engine.BetfairAPI
Imports ATP_Engine.DatabseActions

Public Module GlobalVariables
    Public SessionToken As String
    Public Appkey As String = "ZfI8hcEMs3uAzPmD"
    Public Threadcount As Int16 = 0
End Module '

Module Module1

    Sub Main()

        '-------------------------------------------------------------------------------------------------------------------
        'Pull out MarketId's to update
        '-------------------------------------------------------------------------------------------------------------------


        Dim database As New DatabseActions
        Dim TimeOffset As String = Format(DateAdd(DateInterval.Minute, 10, Date.Now), "HH:mm")
        Dim Time As String = Format(Date.Now, "HH:mm")
        Dim marketIds As DataTable = database.SELECTSTATEMENT("MarketID", "BetfairMarketIds", "WHERE RaceTime > '" & Time & "' AND RaceTime < '" & TimeOffset & "'")
        Dim API As New BetfairAPI


        SessionToken = API.GetSessionKey("ZfI8hcEMs3uAzPmD", "username=00alawre&password=portsmouth1")


        '-------------------------------------------------------------------------------------------------------------------
        '
        'Iterate through row and update odds
        '-------------------------------------------------------------------------------------------------------------------

        For Each marketID_row As DataRow In marketIds.Rows()

            'Call BetfairOddsUpdater(marketID_row)

            Dim Thread As New Threading.Thread(AddressOf BetfairOddsUpdater)
            Thread.IsBackground = False

            Thread.Start(marketID_row)
        Next


    End Sub

    Private Sub BetfairOddsUpdater(ByVal row As DataRow)

        Dim database As New DatabseActions
        Dim API As New BetfairAPI


        'Declare marketIDs and get Json for horse names and prices
        Dim marketID As String = row.Item(0).ToString
        Dim priceJson As String = API.GetPrices(Appkey, SessionToken, marketID)

        Dim o As JObject = JObject.Parse(priceJson)
        Dim results As List(Of JToken) = o.Children().ToList

        For Each item As JProperty In results
            item.CreateReader()

            If item.Value.Type = JTokenType.Array Then
                For Each selectionID As JObject In item.Values



                    Dim Matched As String = selectionID("totalMatched")

                    Dim runners As List(Of JToken) = selectionID.Children().ToList

                    For Each runner As JProperty In runners
                        runner.CreateReader()

                        If runner.Value.Type = JTokenType.Array Then

                            For Each horse As JObject In runner.Values

                                Dim values As String = ""

                                Dim Status As String = horse("status")

                                If Not Status = "REMOVED" Then

                                    Dim lastprice As Decimal = 0
                                    Dim selection_TotalMatched As Decimal = 0

                                    Dim selection As String = horse("selectionId")

                                    If Not IsNothing(horse("lastPriceTraded")) Then
                                        lastprice = horse("lastPriceTraded")

                                    Else

                                        lastprice = 0

                                    End If

                                    If Not IsNothing(horse("totalMatched")) Then

                                        selection_TotalMatched = horse("totalMatched")

                                    Else

                                        selection_TotalMatched = 0

                                    End If




                                    Using con As New SqlConnection(System.Configuration.ConfigurationManager.ConnectionStrings("PuntersEdgeDB").ConnectionString)

                                            Dim comm As New SqlCommand

                                            comm.CommandText = "EXECUTE ATP_Engine @Price=" & lastprice & ", @Selection='" & selection & "', @MarketID ='" & marketID & "'"

                                            comm.Connection = con

                                            con.Open()
                                            comm.ExecuteNonQuery()
                                            con.Close()

                                        End Using


                                    Else

                                        Using con As New SqlConnection(System.Configuration.ConfigurationManager.ConnectionStrings("PuntersEdgeDB").ConnectionString)

                                        Dim selection As String = horse("selectionId")
                                        Dim comm As New SqlCommand

                                        comm.CommandText = "UPDATE BetfairData
                                                            SET Price10 = 0,
		                                                    Price9 = 0,
		                                                    Price8 = 0,
		                                                    Price7 = 0,
		                                                    Price6 = 0,
		                                                    Price5 = 0,
		                                                    Price4 = 0,
		                                                    Price3 = 0,
		                                                    Price2 = 0,
		                                                    LastTradedPrice = 0

                                                            WHERE SelectionID = " & selection & "
                                                             MarketID = " & marketID


                                        comm.Connection = con

                                        con.Open()
                                        comm.ExecuteNonQuery()
                                        con.Close()

                                    End Using

                                End If


                            Next

                        End If

                    Next

                Next

            End If

        Next

        Dim db As New DatabseActions
        db.EXECSPROC("RunLiveSelections", "")
        db.EXECSPROC("v2_RunLiveSelections", "")

    End Sub

End Module
