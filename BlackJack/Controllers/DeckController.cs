using BlackJack.Models;
using BlackJack.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BlackJack.Controllers
{
    //use blackjack in route since we named controller "DeckController"
    [Route("blackjack")]
    [ApiController]
    public class DeckController : ControllerBase
    {
        //static GameStatus status = new GameStatus();
        static List<GameStatus> allGames = new List<GameStatus>();
        static int nextId = 1;

        private readonly DeckService _service;
        public DeckController(DeckService service)
        {
            _service = service;
        }

        [HttpGet("AllHands")]
        public IActionResult GetAll()
        {
            return Ok(allGames);
        }

        [HttpGet()]
        public async Task<IActionResult> GetGame(int gameId)
        {
            GameStatus status = allGames.FirstOrDefault(g => g.id == gameId);
            if (status.DeckId == null)
            {
                return NotFound("No game started");
            }
            return Ok(status);
        }

        [HttpPost()]
        public async Task<IActionResult> NewGame(int? gameId = null)
        {
            GameStatus status;
            if (gameId != null)
            {
            status = allGames.FirstOrDefault(g => g.id == gameId);
            if (status.GameOver == false && status.DeckId != null)
            {
                return Conflict("Game in progress.");
            }

            }
            DeckModel newDeck = await _service.NewDeck();
            status = new GameStatus();
            allGames.Add(status);
            status.id = nextId++;
            status.DeckId = newDeck.deck_id;
            DeckModel resultCards = await _service.DrawCards(3, status.DeckId);
            status.DealerCards = new List<Card>() { resultCards.cards[0] };
            status.PlayerCards = new List<Card>() { resultCards.cards[1], resultCards.cards[2] };
            status.DealerScore = GetHandScore(status.DealerCards);
            status.PlayerScore = GetHandScore(status.PlayerCards);
            status.GameOver = false;
            status.Outcome = "";

            //for if two aces draw at first
            if (status.PlayerScore > 21)
            {
                status.GameOver = true;
                status.Outcome = "Bust";
            }

            return Created("", status);
        }

        [HttpPost("play")]
        public async Task<IActionResult> GameAction(int gameId, string action)
        {
            GameStatus status = allGames.FirstOrDefault(g => g.id == gameId);
            if (status.DeckId == null)
            {
                return NotFound("No game started");
            }
            if (status.GameOver == true)
            {
                return Conflict("Game in progress.");
            }

            action = action.ToLower().Trim();

            if (action == "hit")
            {
                DeckModel cardsResult = await _service.DrawCards(1, status.DeckId);
                status.PlayerCards.Add(cardsResult.cards[0]);
                status.PlayerScore += GetHandScore(status.PlayerCards);
                if(status.PlayerScore > 21)
                {
                    status.GameOver = true;
                    status.Outcome = "Bust";
                }
            }
            else if(action == "stand")
            {
                status.GameOver = true;
                while(status.DealerScore < 17)
                {
                DeckModel cardsResult = await _service.DrawCards(1, status.DeckId);
                status.DealerCards.Add(cardsResult.cards[0]);
                status.DealerScore += GetHandScore(status.DealerCards);
                }
                //status.GameOver = true;
                if(status.PlayerScore == 21 && status.DealerScore != 21 || status.PlayerScore > status.DealerScore)
                {
                    status.Outcome = "Win";
                }
                else if(status.PlayerScore != 21 && status.DealerScore == 21 || status.PlayerScore < status.DealerScore)
                {
                    status.Outcome = "Loss";
                }
                else
                {
                    status.Outcome = "Standoff";
                }
            }
            else
            {
                return BadRequest($"Invalid Action: {action}");
            }
            return Ok(status);
        }

        private int GetHandScore(List<Card> targetCards)
        {
            int total = targetCards.Sum(c => GetCardScore(c));
            int aces = targetCards.Sum(c => c.value == "ACE"? 1: 0);
            for (int i = 0; i < aces; i++)
            {
                if(total > 21)
                {
                    total -= 10;
                }
            }
            return total;
        }

        private int GetCardScore(Card c)
        {
            if(c.value == "ACE")
            {
                return 11;
            }
            else if(c.value == "KING" || c.value == "QUEEN" || c.value == "JACK")
            {
                return 10;
            }
            else if(c.value == "JOKER")
            {
                return 0;
            }
            else
            {
                return int.Parse(c.value);
            }
        }
    }
}
