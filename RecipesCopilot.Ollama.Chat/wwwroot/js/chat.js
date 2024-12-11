document.addEventListener('DOMContentLoaded', function () {
    const connection = new signalR.HubConnectionBuilder()
        .withUrl('/chathub')
        .build();

    const token = document.querySelector('input[name="__RequestVerificationToken"]').value;


    let assistantMessageElement = null;
    let assistantMessageText = null;

    connection.on('ReceiveMessage', function (sender, message, done) {
        const chatHistory = document.getElementById('chatHistory');

        if (sender === 'User') {
            // Reset assistant message tracking when a new user message is sent
            assistantMessageElement = null;
            assistantMessageText = null;

            // Create a new message element for the user
            const messageElement = document.createElement('div');
            messageElement.classList.add('message', 'user');

            const messageContent = document.createElement('div');
            messageContent.classList.add('message-content');

            const messageText = document.createElement('p');
            messageText.textContent = message;

            const icon = document.createElement('img');
            icon.src = '/images/user-icon.png';
            icon.alt = 'User Icon';
            icon.classList.add('user-icon');

            messageContent.appendChild(messageText);
            messageElement.appendChild(messageContent);
            messageElement.appendChild(icon);

            chatHistory.appendChild(messageElement);
        } else if (sender === 'LLM') {
            if (!assistantMessageElement) {

                console.log('Creating new assistant message element');
                // Create a new message element for the assistant when the first chunk arrives
                assistantMessageElement = document.createElement('div');
                assistantMessageElement.classList.add('message', 'llm');

                const messageContent = document.createElement('div');
                messageContent.classList.add('message-content');

                assistantMessageText = document.createElement('p');
                assistantMessageText.textContent = '';

                const icon = document.createElement('img');
                icon.src = '/images/llm-icon.png';
                icon.alt = 'Assistant Icon';
                icon.classList.add('llm-icon');

                messageContent.appendChild(assistantMessageText);
                assistantMessageElement.appendChild(icon);
                assistantMessageElement.appendChild(messageContent);

                chatHistory.appendChild(assistantMessageElement);
            }

            // Append the new chunk to the existing assistant message
            assistantMessageText.textContent += message;

            if (done) {
                // Assistant's response is complete; reset tracking variables
                assistantMessageElement = null;
                assistantMessageText = null;
            }
        }

        // Scroll to the bottom
        chatHistory.scrollTop = chatHistory.scrollHeight;
    });

    connection.start().then(function () {
        console.log('SignalR Connected');
    }).catch(function (err) {
        return console.error(err.toString());
    });

    const chatForm = document.getElementById('chatForm');
    chatForm.addEventListener('submit', function (e) {
        e.preventDefault();
        const userMessage = document.getElementById('userMessage').value;
        document.getElementById('userMessage').value = '';

        fetch('/Home/SendMessage', {
            method: 'POST',
            body: new URLSearchParams({ userMessage: userMessage }),
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded'
            }
        }).catch(function (error) {
            console.error('Error:', error);
        });
    });

    // Add event listener for the Clear button
    const clearButton = document.getElementById('clearChat');
    clearButton.addEventListener('click', function () {
        // Send a POST request to the ClearChat action
        fetch('/Home/ClearChat', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded',
                'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]').value
            }
        })
            .then(response => {
                if (response.ok) {
                    // Clear the chat history in the UI
                    const chatHistory = document.getElementById('chatHistory');
                    chatHistory.innerHTML = '';

                    // Reset assistant message tracking variables
                    assistantMessageElement = null;
                    assistantMessageText = null;
                } else {
                    console.error('Failed to clear chat history');
                }
            })
            .catch(error => {
                console.error('Error:', error);
            });
    });
});
