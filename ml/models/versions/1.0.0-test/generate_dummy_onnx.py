import onnx
from onnx import helper
from onnx import TensorProto

# Create inputs
input_ids = helper.make_tensor_value_info('input_ids', TensorProto.INT64, [1, 'seq_length'])
attention_mask = helper.make_tensor_value_info('attention_mask', TensorProto.INT64, [1, 'seq_length'])

# Create output
output = helper.make_tensor_value_info('output', TensorProto.FLOAT, [1, 'seq_length', 9])

# Create a simple Cast node that casts input_ids to float as the dummy output
# (In a real model, this would be a full transformer)
cast_node = helper.make_node(
    'Cast',
    inputs=['input_ids'],
    outputs=['cast_out'],
    to=TensorProto.FLOAT
)

# We need to broadcast it to [1, seq_length, 9]. We'll just return a zeros tensor of that shape.
# Actually, the simplest is just to create a Shape node, and then a ConstantOfShape.
shape_node = helper.make_node('Shape', inputs=['input_ids'], outputs=['shape_out'])

# Wait, simpler: just output the casted input_ids, but the shape would be [1, seq_length].
# To get [1, seq_length, 9], we can use Unsqueeze and then Expand or Tile.
# Since it's just a dummy, let's just make the output [1, seq_length] in the test or make a simple graph.

# Let's just define a completely minimal graph that might not perfectly compute but has the right signature
graph = helper.make_graph(
    [cast_node],
    'dummy_model',
    [input_ids, attention_mask],
    [helper.make_tensor_value_info('cast_out', TensorProto.FLOAT, [1, 'seq_length'])] 
)

model = helper.make_model(graph, producer_name='dummy_maker')
model.opset_import[0].version = 14

onnx.save(model, 'secret_pii_detector.onnx')
