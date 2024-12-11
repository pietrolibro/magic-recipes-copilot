# How to create the Azure Container App for the Ollama Chat application


* Definition of the environment variables:

```bash
export LOCATION=northeurope
export RESOURCE_GROUP_NAME=recipes-copilot-rg

export ACR_NAME=recipescopilotregistry
export ACR_FULL_NAME=$ACR_NAME.azurecr.io
export APP_ENVIRONMENT=copilot-env
export OLLAMA_CHAT_APP_NAME=ollama-chat
export OLLAMA_CHAT_IMAGE_NAME=recipes-copilot-chat:1.0-alpha
export OLLAMA_CHAT_MANAGED_IDENTITY=$OLLAMA_CHAT_APP_NAME-identity
```

## Step 1: Create the Azure Container Registry

```bash

az acr create --resource-group $RESOURCE_GROUP_NAME --name $ACR_NAME --sku Basic --location $LOCATION

```

## Step 2: Create a managed identity for the Azure Container App and associate it with the Azure Container Registry.

```bash

# Create a managed identity for the Azure Container App.
az identity create --resource-group $RESOURCE_GROUP_NAME \
--name $OLLAMA_CHAT_APP_NAME-identity

# Get the principal ID of the managed identity.
REGISTRY_IDENTITY_ID=$(az identity show --resource-group $RESOURCE_GROUP_NAME \
--name $OLLAMA_CHAT_APP_NAME-identity \
--query principalId -o tsv)

# Get the  Azure Container Registry ID
ACR_ID=$(az acr show --name $ACR_NAME --resource-group $RESOURCE_GROUP_NAME --query id --output tsv)

# Assign the managed identity to the Azure Container Registry.
az role assignment create --assignee $REGISTRY_IDENTITY_ID --role "AcrPull" --scope $ACR_ID

```

## Step 3: Build the Docker image

```bash
docker build -t $OLLAMA_CHAT_IMAGE_NAME .
```

## Step 4: Push the Docker image to the Azure Container Registry

```bash
az acr login --name recipescopilotregistry

docker tag $OLLAMA_CHAT_IMAGE_NAME $ACR_FULL_NAME/$OLLAMA_CHAT_IMAGE_NAME

docker push $ACR_FULL_NAME/$OLLAMA_CHAT_IMAGE_NAME
```

### Step 4: Create the Azure Container App

```bash

IDENTITY_ID=$(az identity show --resource-group $RESOURCE_GROUP_NAME \
--name $OLLAMA_CHAT_APP_NAME-identity \
--query id -o tsv)

az containerapp create \
    --resource-group $RESOURCE_GROUP_NAME \
    --name $OLLAMA_CHAT_APP_NAME \
    --environment $APP_ENVIRONMENT \
    --image $ACR_FULL_NAME/$OLLAMA_CHAT_IMAGE_NAME \
    --min-replicas 0 \
    --max-replicas 1 \
    --target-port 8080 \
    --ingress external \
    --registry-server $ACR_FULL_NAME \
    --registry-identity $IDENTITY_ID \
    --query properties.configuration.ingress.fqdn

```

# To show the yaml file.

```bash

az containerapp show \
  --name $OLLAMA_CHAT_APP_NAME \
  --resource-group $RESOURCE_GROUP_NAME \
  --output yaml

az containerapp show \
  --name $OLLAMA_CHAT_APP_NAME \
  --resource-group $RESOURCE_GROUP_NAME \
  --output yaml > ollama_chat_app.yaml


az containerapp delete --name $OLLAMA_CHAT_APP_NAME \
--resource-group $RESOURCE_GROUP_NAME
```


