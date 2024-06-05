
from openai import OpenAI
import base64


client = OpenAI( api_key="THIS_NEEDS_TO_BE_VALID_API")

#image_prompt = "Generate an image about What are some of the less common but domesticated breeds of horses?"

image_prompt = """
generate a photograph about a Marmot, indoor habitat, cozy den, fluffy bedding, rocky climbing structures, bowl of fresh herbs, water fountain, playful marmot, curious expression, soft lighting, warm and inviting
"""

image_prompt = """
generate a photograph about A courageous woman rappels down a sheer rock face into a shadowy cave, adrenaline pumping, hands gripping the rope tightly, Light reflecting off the water below, Cascading drips,
Detailed rock formations, Rough textures, Dramatic lighting, Sense of suspense, In the style of a Canon EOS-1D X Mark II --s 50 --v 6.0
"""

response = client.images.generate(
  model="dall-e-3",
  prompt=image_prompt,
  size="1024x1024",
  quality="standard",
  response_format="b64_json",
  n=1,
)

#image_url = response.data[0].url
image_data = response.data[0].b64_json



with open("output.png", 'wb') as f:
  f.write(base64.b64decode(image_data))






