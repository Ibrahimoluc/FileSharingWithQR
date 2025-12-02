using System.IO.Pipes;

namespace FileSharingWithQR.Services
{
    public class PageTokenManager
    {
        public static string GetPageToken(Stack<string> stack, int PageDirection)
        {
            if (PageDirection == 1)
            {
                //Next Page
                Console.WriteLine("Stack count:" + stack.Count);
                foreach (var item in stack)
                {
                    Console.WriteLine($"{item} ");
                }
                return stack.Peek();
            }
            else if (PageDirection == 0)
            {
                //Current Page
                stack.Pop();
                return stack.Peek();
            }
            else if (PageDirection == -1)
            {
                //Prev Page
                stack.Pop();
                stack.Pop();
                return stack.Peek();
            }
            else
            {
                throw new ArgumentException("PageDirection parameter should one of them: {1,0,-1}");
            }
        }


    }
}

