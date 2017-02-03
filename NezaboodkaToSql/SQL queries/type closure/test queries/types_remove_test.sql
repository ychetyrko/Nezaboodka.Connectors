/*======================================
		Remove fields tests
 (based on `Types ordering tests`
  and `Add types and fields tests`)
======================================*/

USE `nz_test_closure`;

/*---------------------------------------/
		Remove full hierarchy
--------------------------------------*/
CALL before_alter_types();
INSERT INTO `type_rem_list`
(`name`)
VALUES
('Moderator'),
('Admin'),
('UberAdmin'),
('Group'),
('User');
CALL remove_types();

/*---------------------------------------/
		Remove terminating types
--------------------------------------*/
CALL before_alter_types();
INSERT INTO `type_rem_list`
(`name`)
VALUES
('CoolChopper'),
('Sedan');
CALL remove_types();

/*---------------------------------------/
		Remove referenced type
			[Error expected]
--------------------------------------*/
CALL before_alter_types();
INSERT INTO `type_rem_list`
(`name`)
VALUES
('Car');	-- referenced by VeryGoodPeople
CALL remove_types();
